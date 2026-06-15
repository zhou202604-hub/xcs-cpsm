using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kingdee.MaterialAPI.Services;
using Microsoft.IdentityModel.Tokens;

namespace Kingdee.MaterialAPI;

/// <summary>
/// 企业微信认证服务
/// - OAuth2 授权：通过 code 换取 userid / 用户信息
/// - JWT 签发：登录成功后下发 Token
/// - JS-SDK 签名：返回 wx.config 需要的 signature 等
/// - 配置热更新：所有配置从 AppConfigStore 读取，管理后台保存后立即生效
/// </summary>
public class WeComAuthService
{
    private readonly AppConfigStore _config;
    private readonly HttpClient _http;
    private readonly ILogger<WeComAuthService> _logger;

    // 缓存（access_token/jsapi_ticket 有效期一般 7200s）
    private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpireAt)> _tokenCache = new();
    private static readonly ConcurrentDictionary<string, (string Ticket, DateTime ExpireAt)> _ticketCache = new();

    public WeComAuthService(AppConfigStore config, ILogger<WeComAuthService> logger)
    {
        _config = config;
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10), BaseAddress = new Uri("https://qyapi.weixin.qq.com") };
    }

    /// <summary>当前配置是否已启用企业微信且设置了 corpId/secret</summary>
    public bool IsConfigured
    {
        get
        {
            var c = _config.Get();
            return c.WeComEnable &&
                   !string.IsNullOrWhiteSpace(c.WeComCorpId) &&
                   !string.IsNullOrWhiteSpace(c.WeComSecret);
        }
    }

    /// <summary>构造企业微信授权跳转 URL（OAuth2 scope=snsapi_base）</summary>
    public string BuildOAuthUrl(string? redirectAfter = null)
    {
        var cfg = _config.Get();
        var state = string.IsNullOrWhiteSpace(redirectAfter) ? "home"
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(redirectAfter));
        var redirect = Uri.EscapeDataString(cfg.WeComCallbackUrl);
        return $"https://open.weixin.qq.com/connect/oauth2/authorize?appid={cfg.WeComCorpId}&redirect_uri={redirect}&response_type=code&scope=snsapi_base&state={state}#wechat_redirect";
    }

    /// <summary>用 code 换 userid</summary>
    public async Task<string?> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token)) return null;
            var resp = await _http.GetAsync($"/cgi-bin/user/getuserinfo?access_token={token}&code={code}", ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);
            var data = System.Text.Json.JsonDocument.Parse(body);
            if (data.RootElement.TryGetProperty("errcode", out var ec) && ec.GetInt32() != 0)
            {
                _logger.LogWarning("企业微信 getuserinfo 失败: {Body}", body);
                return null;
            }
            return data.RootElement.TryGetProperty("UserId", out var uid) ? uid.GetString() : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExchangeCodeAsync 异常");
            return null;
        }
    }

    /// <summary>根据 userid 拉成员姓名</summary>
    public async Task<string> GetUserNameAsync(string userid, CancellationToken ct = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token)) return userid;
            var resp = await _http.GetAsync($"/cgi-bin/user/get?access_token={token}&userid={Uri.EscapeDataString(userid)}", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            var data = System.Text.Json.JsonDocument.Parse(body);
            return data.RootElement.TryGetProperty("name", out var n) ? (n.GetString() ?? userid) : userid;
        }
        catch
        {
            return userid;
        }
    }

    /// <summary>签发 JWT（userid/name）</summary>
    public string IssueJwt(string userid, string name)
    {
        var cfg = _config.Get();
        var key = Encoding.ASCII.GetBytes(string.IsNullOrWhiteSpace(cfg.WeComJwtSecret) ? "dev-placeholder-change-me" : cfg.WeComJwtSecret);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userid),
            new Claim(ClaimTypes.Name, name),
            new Claim("corpid", cfg.WeComCorpId ?? "")
        };
        var desc = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(cfg.WeComJwtExpireHours <= 0 ? 24 : cfg.WeComJwtExpireHours),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = "Kingdee.MaterialAPI",
            Audience = "WeComUser"
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(desc));
    }

    /// <summary>验证 JWT，返回 (userid,name)</summary>
    public (bool Ok, string UserId, string Name) ValidateJwt(string token)
    {
        try
        {
            var cfg = _config.Get();
            var key = Encoding.ASCII.GetBytes(string.IsNullOrWhiteSpace(cfg.WeComJwtSecret) ? "dev-placeholder-change-me" : cfg.WeComJwtSecret);
            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = "Kingdee.MaterialAPI",
                ValidAudience = "WeComUser",
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.FromMinutes(5)
            }, out var validated);

            var jwt = (JwtSecurityToken)validated;
            var uid = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "";
            var name = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "";
            return (true, uid, name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT 校验失败");
            return (false, "", "");
        }
    }

    // ======== AccessToken / JsApiTicket （带内存缓存） =========
    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var cfg = _config.Get();
        var key = $"wx_token|{cfg.WeComCorpId}|{cfg.WeComSecret.GetHashCode()}";
        if (_tokenCache.TryGetValue(key, out var cached) && cached.ExpireAt > DateTime.Now)
            return cached.Token;

        try
        {
            var resp = await _http.GetAsync($"/cgi-bin/gettoken?corpid={Uri.EscapeDataString(cfg.WeComCorpId)}&corpsecret={Uri.EscapeDataString(cfg.WeComSecret)}", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            var data = System.Text.Json.JsonDocument.Parse(body);
            if (data.RootElement.TryGetProperty("errcode", out var ec) && ec.GetInt32() != 0)
            {
                _logger.LogWarning("企业微信 gettoken 失败: {Body}", body);
                return "";
            }
            var token = data.RootElement.GetProperty("access_token").GetString() ?? "";
            var expireIn = 3600;
            if (data.RootElement.TryGetProperty("expires_in", out var exp))
                int.TryParse(exp.ToString(), out expireIn);
            _tokenCache[key] = (token, DateTime.Now.AddSeconds(expireIn - 120));
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAccessTokenAsync 异常");
            return "";
        }
    }

    public async Task<string> GetJsApiTicketAsync(CancellationToken ct = default)
    {
        var cfg = _config.Get();
        var key = $"wx_ticket|{cfg.WeComCorpId}|{cfg.WeComSecret.GetHashCode()}";
        if (_ticketCache.TryGetValue(key, out var cached) && cached.ExpireAt > DateTime.Now)
            return cached.Token;

        try
        {
            var token = await GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token)) return "";
            var resp = await _http.GetAsync($"/cgi-bin/get_jsapi_ticket?access_token={token}", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            var data = System.Text.Json.JsonDocument.Parse(body);
            if (data.RootElement.TryGetProperty("errcode", out var ec) && ec.GetInt32() != 0)
            {
                _logger.LogWarning("企业微信 get_jsapi_ticket 失败: {Body}", body);
                return "";
            }
            var ticket = data.RootElement.GetProperty("ticket").GetString() ?? "";
            var expireIn = 3600;
            if (data.RootElement.TryGetProperty("expires_in", out var exp))
                int.TryParse(exp.ToString(), out expireIn);
            _ticketCache[key] = (ticket, DateTime.Now.AddSeconds(expireIn - 120));
            return ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetJsApiTicketAsync 异常");
            return "";
        }
    }

    /// <summary>为当前 URL 生成 JS-SDK 签名参数</summary>
    public async Task<(string AppId, ulong Timestamp, string NonceStr, string Signature, int ErrCode, string ErrMsg)> BuildJsSdkSignAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var cfg = _config.Get();
            var ticket = await GetJsApiTicketAsync(ct);
            if (string.IsNullOrWhiteSpace(ticket))
                return (cfg.WeComCorpId, 0, "", "", 500, "无法获取 jsapi_ticket");
            var nonce = Guid.NewGuid().ToString("N").Substring(0, 16);
            var timestamp = (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            var raw = $"jsapi_ticket={ticket}&noncestr={nonce}&timestamp={timestamp}&url={url}";
            return (cfg.WeComCorpId, timestamp, nonce, Sha1(raw), 0, "OK");
        }
        catch (Exception ex)
        {
            var cfg = _config.Get();
            _logger.LogError(ex, "BuildJsSdkSign 异常");
            return (cfg.WeComCorpId, 0, "", "", 500, ex.Message);
        }
    }

    private static string Sha1(string input)
    {
        using var sha = SHA1.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder();
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

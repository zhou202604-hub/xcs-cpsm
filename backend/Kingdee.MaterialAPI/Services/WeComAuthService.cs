using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kingdee.MaterialAPI.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kingdee.MaterialAPI.Services;

/// <summary>
/// 企业微信认证服务
/// - OAuth2 授权：通过 code 换取 userid / 用户信息
/// - JWT 签发：登录成功后下发 Token
/// - JS-SDK 签名：提供 wx.config 需要的 signature
/// </summary>
public class WeComAuthService
{
    private readonly WeComSettings _settings;
    private readonly HttpClient _http;
    private readonly ILogger<WeComAuthService> _logger;

    // 简单内存缓存（AccessToken 默认 7200 秒；JsapiTicket 同样）
    private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpireAt)> _tokenCache = new();
    private static readonly ConcurrentDictionary<string, (string Ticket, DateTime ExpireAt)> _ticketCache = new();

    // 用于 JWT 的序列化 JsonSerializerOptions
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public WeComAuthService(IOptions<WeComSettings> settings, ILogger<WeComAuthService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
            BaseAddress = new Uri("https://qyapi.weixin.qq.com")
        };
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.CorpId) &&
        !string.IsNullOrWhiteSpace(_settings.Secret) &&
        !string.IsNullOrWhiteSpace(_settings.JwtSecret);

    public WeComSettings CurrentSettings => _settings;

    /// <summary>
    /// 生成企业微信 OAuth2 授权跳转 URL
    /// </summary>
    public string BuildOAuthUrl(string? redirectAfter = null)
    {
        var state = string.IsNullOrWhiteSpace(redirectAfter) ? "home" : Convert.ToBase64String(Encoding.UTF8.GetBytes(redirectAfter));
        var redirect = Uri.EscapeDataString(_settings.CallbackUrl);
        return $"https://open.weixin.qq.com/connect/oauth2/authorize?appid={_settings.CorpId}&redirect_uri={redirect}&response_type=code&scope=snsapi_base&state={state}#wechat_redirect";
    }

    /// <summary>
    /// 用 code 换 userid
    /// </summary>
    public async Task<(string UserId, string? DeviceId)?> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token)) return null;

            var url = $"/cgi-bin/user/getuserinfo?access_token={token}&code={code}";
            var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonDocument.Parse(body);

            var errCode = data.RootElement.GetProperty("errcode").GetInt32();
            if (errCode != 0)
            {
                _logger.LogError("企业微信 getuserinfo 失败: {Body}", body);
                return null;
            }

            var userId = data.RootElement.GetProperty("UserId").GetString();
            var deviceId = data.RootElement.TryGetProperty("DeviceId", out var dev)
                ? dev.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(userId)) return null;
            return (userId, deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "企业微信 ExchangeCode 异常");
            return null;
        }
    }

    /// <summary>
    /// 根据 userid 获取成员姓名/部门等基本信息
    /// </summary>
    public async Task<(string Name, string Department, string Avatar, string Mobile)?> GetUserInfoAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var token = await GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token)) return null;

            var url = $"/cgi-bin/user/get?access_token={token}&userid={Uri.EscapeDataString(userId)}";
            var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonDocument.Parse(body);

            var errCode = data.RootElement.GetProperty("errcode").GetInt32();
            if (errCode != 0)
            {
                _logger.LogError("企业微信 user/get 失败: {Body}", body);
                return null;
            }

            var name = data.RootElement.GetProperty("name").GetString() ?? userId;
            var avatar = data.RootElement.TryGetProperty("avatar", out var av) ? av.GetString() ?? "" : "";
            var mobile = data.RootElement.TryGetProperty("mobile", out var mb) ? mb.GetString() ?? "" : "";
            var dept = "";
            if (data.RootElement.TryGetProperty("department", out var deptEl) && deptEl.ValueKind == JsonValueKind.Array)
            {
                dept = string.Join(",", deptEl.EnumerateArray().Select(x => x.GetInt32().ToString()));
            }

            return (name, dept, avatar, mobile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "企业微信 GetUserInfo 异常");
            return null;
        }
    }

    /// <summary>
    /// 签发 JWT Token（含 userid/name 等 claim）
    /// </summary>
    public string IssueJwt(string userid, string name)
    {
        var key = Encoding.ASCII.GetBytes(_settings.JwtSecret);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userid),
            new Claim(ClaimTypes.Name, name),
            new Claim("corpid", _settings.CorpId)
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(_settings.JwtExpireHours <= 0 ? 24 : _settings.JwtExpireHours),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = "Kingdee.MaterialAPI",
            Audience = "WeComUser"
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// 验证 JWT，成功时返回 userid / name
    /// </summary>
    public (bool Ok, string UserId, string Name) ValidateJwt(string token)
    {
        try
        {
            var key = Encoding.ASCII.GetBytes(_settings.JwtSecret);
            var tokenHandler = new JwtSecurityTokenHandler();
            tokenHandler.ValidateToken(token, new TokenValidationParameters
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
            var userId = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "";
            var name = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "";
            return (true, userId, name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT 校验失败");
            return (false, "", "");
        }
    }

    /// <summary>
    /// 获取 AccessToken（带缓存）
    /// </summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var cacheKey = "access_token:" + _settings.CorpId + ":" + _settings.Secret.GetHashCode();
        if (_tokenCache.TryGetValue(cacheKey, out var cached) && cached.ExpireAt > DateTime.Now)
        {
            return cached.Token;
        }

        try
        {
            var url = $"/cgi-bin/gettoken?corpid={Uri.EscapeDataString(_settings.CorpId)}&corpsecret={Uri.EscapeDataString(_settings.Secret)}";
            var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonDocument.Parse(body);

            var errCode = data.RootElement.GetProperty("errcode").GetInt32();
            if (errCode != 0)
            {
                _logger.LogError("企业微信 gettoken 失败: {Body}", body);
                return string.Empty;
            }

            var token = data.RootElement.GetProperty("access_token").GetString() ?? "";
            var expiresIn = data.RootElement.TryGetProperty("expires_in", out var exp)
                ? exp.GetInt32()
                : 7200;

            _tokenCache[cacheKey] = (token, DateTime.Now.AddSeconds(expiresIn - 120));
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "企业微信 GetAccessToken 异常");
            return string.Empty;
        }
    }

    /// <summary>
    /// 获取 JS-SDK 所需的 jsapi_ticket（带缓存）
    /// </summary>
    public async Task<string> GetJsApiTicketAsync(CancellationToken ct = default)
    {
        var cacheKey = "ticket:" + _settings.CorpId + ":" + _settings.Secret.GetHashCode();
        if (_ticketCache.TryGetValue(cacheKey, out var cached) && cached.ExpireAt > DateTime.Now)
        {
            return cached.Ticket;
        }

        try
        {
            var token = await GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token)) return string.Empty;

            var url = $"/cgi-bin/get_jsapi_ticket?access_token={token}";
            var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonDocument.Parse(body);

            var errCode = data.RootElement.GetProperty("errcode").GetInt32();
            if (errCode != 0)
            {
                _logger.LogError("企业微信 get_jsapi_ticket 失败: {Body}", body);
                return string.Empty;
            }

            var ticket = data.RootElement.GetProperty("ticket").GetString() ?? "";
            var expiresIn = data.RootElement.TryGetProperty("expires_in", out var exp)
                ? exp.GetInt32()
                : 7200;

            _ticketCache[cacheKey] = (ticket, DateTime.Now.AddSeconds(expiresIn - 120));
            return ticket;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "企业微信 GetJsApiTicket 异常");
            return string.Empty;
        }
    }

    /// <summary>
    /// 生成 wx.config 需要的签名参数
    /// </summary>
    public async Task<(string AppId, ulong Timestamp, string NonceStr, string Signature, int ErrCode, string ErrMsg)> BuildJsSdkSignAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var ticket = await GetJsApiTicketAsync(ct);
            if (string.IsNullOrWhiteSpace(ticket))
                return (_settings.CorpId, 0, "", "", 500, "无法获取 jsapi_ticket");

            var nonce = Guid.NewGuid().ToString("N").Substring(0, 16);
            var timestamp = (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            // 签名串：按字段 ASCII 字典序
            var raw = $"jsapi_ticket={ticket}&noncestr={nonce}&timestamp={timestamp}&url={url}";
            var signature = Sha1(raw);

            return (_settings.CorpId, timestamp, nonce, signature, 0, "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BuildJsSdkSign 异常");
            return (_settings.CorpId, 0, "", "", 500, ex.Message);
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

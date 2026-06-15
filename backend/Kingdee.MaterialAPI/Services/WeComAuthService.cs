using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Kingdee.MaterialAPI.Models;

namespace Kingdee.MaterialAPI.Services;

/// <summary>
/// 企业微信登录 + 自签发 Token 服务
/// 
/// 流程：
///   1. 用户在企业微信点击应用 → 企业微信在后台拼 code
///   2. 跳转到 /api/auth?code=xxx&state=xxx
///   3. 后端使用 code 调 https://qyapi.weixin.qq.com/cgi-bin/user/getuserinfo?access_token=... 拿 userId
///   4. 再用 userid 调 /cgi-bin/user/get 拿到姓名
///   5. 签发自制 Token（HMAC-SHA256） → 写 cookie 或让前端保存
///   6. 以后每次请求 API 都带此 Token，用相同密钥验证
///
/// 不配置企业微信时，走"开发调试模式"，默认已登录
/// </summary>
public class WeComAuthService
{
    private readonly AppConfigStore _config;
    private readonly ILogger<WeComAuthService> _logger;
    private readonly HttpClient _http;

    private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpireAt)> _tokenCache = new();

    public WeComAuthService(AppConfigStore config, ILogger<WeComAuthService> logger)
    {
        _config = config;
        _logger = logger;
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
            BaseAddress = new Uri("https://qyapi.weixin.qq.com")
        };
    }

    public bool IsWeComConfigured
    {
        get
        {
            var c = _config.Get();
            return c.WeCom.Enable &&
                   !string.IsNullOrWhiteSpace(c.WeCom.CorpId) &&
                   !string.IsNullOrWhiteSpace(c.WeCom.Secret);
        }
    }

    public (bool Enable, string CorpId, string CallbackUrl) CurrentConfig()
    {
        var c = _config.Get();
        return (c.WeCom.Enable, c.WeCom.CorpId, c.WeCom.CallbackUrl);
    }

    public string BuildOAuthUrl()
    {
        var c = _config.Get().WeCom;
        var redirect = Uri.EscapeDataString(c.CallbackUrl);
        return $"https://open.weixin.qq.com/connect/oauth2/authorize?appid={c.CorpId}&redirect_uri={redirect}&response_type=code&scope=snsapi_base&state=home#wechat_redirect";
    }

    public async Task<(string UserId, string Name)> ExchangeCodeGetUser(string code)
    {
        try
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return ("", "");

            // 第一步：code → userId
            var resp1 = await _http.GetAsync($"/cgi-bin/user/getuserinfo?access_token={token}&code={code}");
            if (!resp1.IsSuccessStatusCode) return ("", "");
            var json1 = await resp1.Content.ReadAsStringAsync();
            using var doc1 = System.Text.Json.JsonDocument.Parse(json1);
            if (!doc1.RootElement.TryGetProperty("UserId", out var uidEl)) return ("", "");
            var uid = uidEl.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(uid)) return ("", "");

            // 第二步：userId → name
            var resp2 = await _http.GetAsync($"/cgi-bin/user/get?access_token={token}&userid={Uri.EscapeDataString(uid)}");
            if (!resp2.IsSuccessStatusCode) return (uid, uid);
            var json2 = await resp2.Content.ReadAsStringAsync();
            using var doc2 = System.Text.Json.JsonDocument.Parse(json2);
            var name = doc2.RootElement.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? uid : uid;
            return (uid, name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "企业微信 ExchangeCode 异常");
            return ("", "");
        }
    }

    public string IssueToken(string userId, string name)
    {
        var cfg = _config.Get().WeCom;
        var secret = cfg.JwtSecret;
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 8)
        {
            // 如果用户没配置，用一个运行时生成的兜底密钥（重启后失效）
            secret = "changeme-please-configure-a-long-secret-" + DateTime.UtcNow.Date.Ticks;
        }
        var expireSeconds = cfg.JwtExpireHours > 0 ? cfg.JwtExpireHours * 3600 : 86400;
        var expire = DateTimeOffset.UtcNow.AddSeconds(expireSeconds).ToUnixTimeSeconds();
        var payload = $"{userId}|{name}|{expire}";
        var signature = HmacSha256Hex(secret, payload);
        var token = $"{ToBase64Url(payload)}.{signature}";
        return token;
    }

    public (bool Ok, string UserId, string Name) ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, "", "");
        var parts = token.Split('.');
        if (parts.Length != 2) return (false, "", "");
        try
        {
            var payload = FromBase64Url(parts[0]);
            var signature = parts[1];
            var segs = payload.Split('|');
            if (segs.Length != 3) return (false, "", "");
            var userId = segs[0];
            var name = segs[1];
            var expireStr = segs[2];
            if (!long.TryParse(expireStr, out var expire)) return (false, "", "");
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expire) return (false, "", "");

            // 再算一次签名
            var cfg = _config.Get().WeCom;
            var secret = string.IsNullOrWhiteSpace(cfg.JwtSecret) || cfg.JwtSecret.Length < 8
                ? "changeme-please-configure-a-long-secret-" + DateTime.UtcNow.Date.Ticks
                : cfg.JwtSecret;
            var expected = HmacSha256Hex(secret, payload);
            if (!string.Equals(expected, signature, StringComparison.Ordinal)) return (false, "", "");

            return (true, userId, name);
        }
        catch
        {
            return (false, "", "");
        }
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var c = _config.Get().WeCom;
        var key = $"wx_{c.CorpId}";
        if (_tokenCache.TryGetValue(key, out var cached) && cached.ExpireAt > DateTime.Now)
            return cached.Token;

        try
        {
            var url = $"/cgi-bin/gettoken?corpid={Uri.EscapeDataString(c.CorpId)}&corpsecret={Uri.EscapeDataString(c.Secret)}";
            var resp = await _http.GetAsync(url);
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var errCode = doc.RootElement.TryGetProperty("errcode", out var ec) ? ec.GetInt32() : -1;
            if (errCode != 0)
            {
                _logger.LogError("企业微信 gettoken 失败: {Json}", json);
                return "";
            }
            var token = doc.RootElement.GetProperty("access_token").GetString() ?? "";
            var expiresIn = 7200;
            if (doc.RootElement.TryGetProperty("expires_in", out var ei) && ei.ValueKind == System.Text.Json.JsonValueKind.Number)
                int.TryParse(ei.ToString(), out expiresIn);
            // 提前 5 分钟过期
            _tokenCache[key] = (token, DateTime.Now.AddSeconds(expiresIn - 300));
            return token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取企业微信 AccessToken 失败");
            return "";
        }
    }

    // --------- 工具 ---------
    private static string HmacSha256Hex(string key, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var sb = new StringBuilder();
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private static string ToBase64Url(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string FromBase64Url(string value)
    {
        var b64 = value.Replace('-', '+').Replace('_', '/');
        var pad = b64.Length % 4;
        if (pad > 0) b64 += new string('=', 4 - pad);
        var bytes = Convert.FromBase64String(b64);
        return Encoding.UTF8.GetString(bytes);
    }
}

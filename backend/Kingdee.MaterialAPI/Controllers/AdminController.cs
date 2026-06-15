using System.Text;
using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kingdee.MaterialAPI.Controllers;

/// <summary>
/// 管理后台 API（PC 端）：
///   POST /api/admin/auth/login  → 登录
///   GET  /api/admin/config     → 获取当前配置（包括金蝶/字段映射/企业微信）
///   POST /api/admin/config/kingdee
///   POST /api/admin/config/field-mappings
///   POST /api/admin/config/wecom
///   POST /api/admin/config/admin-password
///   POST /api/admin/test/kingdee
///   POST /api/admin/test/wecom
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppConfigStore _config;
    private readonly WeComAuthService _weCom;
    private readonly KingdeeApiClient _kingdee;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppConfigStore config, WeComAuthService weCom, KingdeeApiClient kingdee, ILogger<AdminController> logger)
    {
        _config = config;
        _weCom = weCom;
        _kingdee = kingdee;
        _logger = logger;
    }

    /// <summary>管理员登录（POST { password: xxx }）</summary>
    [HttpPost("auth/login")]
    public IActionResult Login([FromBody] AdminLoginRequest? body)
    {
        var cfg = _config.Get();
        var pwd = body?.Password ?? "";
        if (pwd != cfg.AdminPassword)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Ok(new { success = false, msg = "密码错误" });
        }

        // 签发一个简单 token（HMAC 签名）："admin|expireUnixSeconds"
        var secret = (cfg.WeCom.JwtSecret ?? "").Length >= 8 ? cfg.WeCom.JwtSecret : "material-admin-secret-2024";
        var expire = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
        var payload = $"admin|{expire}";
        var sig = HmacSha256Hex(secret, payload);
        var token = $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).Replace('+', '-').Replace('/', '_').TrimEnd('=')}.{sig}";

        return Ok(new { success = true, token });
    }

    /// <summary>返回当前所有配置（密码字段用星号代替）</summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var cfg = _config.Get();
        return Ok(new
        {
            kingdee = new
            {
                enable = cfg.Kingdee.Enable,
                serverUrl = cfg.Kingdee.ServerUrl,
                dbId = cfg.Kingdee.DbId,
                userName = cfg.Kingdee.UserName,
                password = string.IsNullOrWhiteSpace(cfg.Kingdee.Password) ? "" : "******",
                lcId = cfg.Kingdee.LcId,
                timeoutSeconds = cfg.Kingdee.TimeoutSeconds,
                imageServerUrl = cfg.Kingdee.ImageServerUrl,
                materialFormId = cfg.Kingdee.MaterialFormId
            },
            fieldMappings = cfg.FieldMappings,
            weCom = new
            {
                enable = cfg.WeCom.Enable,
                corpId = cfg.WeCom.CorpId,
                agentId = cfg.WeCom.AgentId,
                secret = string.IsNullOrWhiteSpace(cfg.WeCom.Secret) ? "" : "******",
                callbackUrl = cfg.WeCom.CallbackUrl,
                jwtSecret = string.IsNullOrWhiteSpace(cfg.WeCom.JwtSecret) ? "" : "******",
                jwtExpireHours = cfg.WeCom.JwtExpireHours,
                forceWeComLogin = cfg.WeCom.ForceWeComLogin
            },
            adminPassword = string.IsNullOrWhiteSpace(cfg.AdminPassword) ? "" : "******"
        });
    }

    [HttpPost("config/kingdee")]
    public IActionResult SaveKingdee([FromBody] KingdeeConfigPayload body)
    {
        var cfg = _config.Get();
        cfg.Kingdee.Enable = body.Enable;
        cfg.Kingdee.ServerUrl = (body.ServerUrl ?? "").Trim();
        cfg.Kingdee.DbId = (body.DbId ?? "").Trim();
        cfg.Kingdee.UserName = (body.UserName ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(body.Password) && body.Password != "******")
            cfg.Kingdee.Password = body.Password;
        cfg.Kingdee.LcId = body.LcId > 0 ? body.LcId : 2052;
        cfg.Kingdee.TimeoutSeconds = body.TimeoutSeconds > 0 ? body.TimeoutSeconds : 30;
        cfg.Kingdee.ImageServerUrl = (body.ImageServerUrl ?? "").Trim();
        cfg.Kingdee.MaterialFormId = (body.MaterialFormId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(cfg.Kingdee.MaterialFormId)) cfg.Kingdee.MaterialFormId = "BD_MATERIAL";
        _config.Save(cfg);
        _logger.LogInformation("管理后台更新了金蝶配置");
        return Ok(new { success = true, msg = "已保存" });
    }

    [HttpPost("config/field-mappings")]
    public IActionResult SaveFieldMappings([FromBody] List<FieldMappingItem> list)
    {
        if (list == null || list.Count == 0)
            return BadRequest(new { success = false, msg = "字段映射不能为空" });
        var cfg = _config.Get();
        // 去除关键字段 key 为空
        var valid = list.Where(x => !string.IsNullOrWhiteSpace(x.FieldKey)).ToList();
        foreach (var item in valid)
        {
            item.FieldKey = (item.FieldKey ?? "").Trim();
            item.Label = (item.Label ?? "").Trim();
            item.KingdeeField = (item.KingdeeField ?? "").Trim();
        }
        cfg.FieldMappings = valid;
        _config.Save(cfg);
        _logger.LogInformation("管理后台更新了 {N} 条字段映射", valid.Count);
        return Ok(new { success = true, msg = $"已保存 {valid.Count} 条字段映射" });
    }

    [HttpPost("config/wecom")]
    public IActionResult SaveWeCom([FromBody] WeComConfigPayload body)
    {
        var cfg = _config.Get();
        cfg.WeCom.Enable = body.Enable;
        cfg.WeCom.CorpId = (body.CorpId ?? "").Trim();
        cfg.WeCom.AgentId = (body.AgentId ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(body.Secret) && body.Secret != "******")
            cfg.WeCom.Secret = body.Secret;
        cfg.WeCom.CallbackUrl = (body.CallbackUrl ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(body.JwtSecret) && body.JwtSecret != "******")
            cfg.WeCom.JwtSecret = body.JwtSecret;
        cfg.WeCom.JwtExpireHours = body.JwtExpireHours > 0 ? body.JwtExpireHours : 24;
        cfg.WeCom.ForceWeComLogin = body.ForceWeComLogin;
        _config.Save(cfg);
        _logger.LogInformation("管理后台更新了企业微信配置");
        return Ok(new { success = true, msg = "已保存" });
    }

    [HttpPost("config/admin-password")]
    public IActionResult SaveAdminPwd([FromBody] AdminPasswordPayload body)
    {
        if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 4)
            return BadRequest(new { success = false, msg = "密码至少 4 位" });
        var cfg = _config.Get();
        cfg.AdminPassword = body.NewPassword;
        _config.Save(cfg);
        _logger.LogInformation("管理员密码已更新");
        return Ok(new { success = true, msg = "密码已更新" });
    }

    [HttpPost("test/kingdee")]
    public async Task<IActionResult> TestKingdee([FromBody] KingdeeConfigPayload body)
    {
        var url = (body.ServerUrl ?? "").Trim();
        var dbId = (body.DbId ?? "").Trim();
        var user = (body.UserName ?? "").Trim();
        var pwd = body.Password == "******" ? _config.Get().Kingdee.Password : (body.Password ?? "");
        var lcId = body.LcId > 0 ? body.LcId : 2052;
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(dbId) || string.IsNullOrWhiteSpace(user))
            return BadRequest(new { success = false, msg = "服务器地址、账套 ID、用户名必填" });

        var (ok, msg) = await _kingdee.TestLoginAsync(url, dbId, user, pwd, lcId);
        return Ok(new { success = ok, msg });
    }

    [HttpPost("test/wecom")]
    public async Task<IActionResult> TestWeCom([FromBody] WeComConfigPayload body)
    {
        // 用临时配置替换测试
        var secret = body.Secret == "******" ? _config.Get().WeCom.Secret : body.Secret;
        var corpId = (body.CorpId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(corpId) || string.IsNullOrWhiteSpace(secret))
            return BadRequest(new { success = false, msg = "CorpId、Secret 必填" });

        // 简单通过 WeComAuthService 走一遍，看能否拿到 AccessToken（需临时修改配置逻辑，
        // 这里直接用单独请求模拟）：
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10), BaseAddress = new Uri("https://qyapi.weixin.qq.com") };
            var resp = await http.GetAsync($"/cgi-bin/gettoken?corpid={Uri.EscapeDataString(corpId)}&corpsecret={Uri.EscapeDataString(secret)}");
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var ec = doc.RootElement.TryGetProperty("errcode", out var el) ? el.GetInt32() : -1;
            if (ec == 0)
                return Ok(new { success = true, msg = "企业微信配置正确" });
            var errMsg = doc.RootElement.TryGetProperty("errmsg", out var em) ? em.GetString() ?? "" : json;
            return Ok(new { success = false, msg = $"企业微信返回：{errMsg}（code={ec}）" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = ex.Message });
        }
    }

    // ========== 辅助 ==========
    private static string HmacSha256Hex(string key, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var sb = new StringBuilder();
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

public class AdminLoginRequest { public string? Password { get; set; } }
public class AdminPasswordPayload { public string? NewPassword { get; set; } }

public class KingdeeConfigPayload
{
    public bool Enable { get; set; }
    public string? ServerUrl { get; set; }
    public string? DbId { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public int LcId { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string? ImageServerUrl { get; set; }
    public string? MaterialFormId { get; set; }
}

public class WeComConfigPayload
{
    public bool Enable { get; set; }
    public string? CorpId { get; set; }
    public string? AgentId { get; set; }
    public string? Secret { get; set; }
    public string? CallbackUrl { get; set; }
    public string? JwtSecret { get; set; }
    public int JwtExpireHours { get; set; } = 24;
    public bool ForceWeComLogin { get; set; }
}

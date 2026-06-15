using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Kingdee.MaterialAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Kingdee.MaterialAPI.Controllers;

/// <summary>
/// 管理后台 API（PC 端）
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AppConfigStore _config;
    private readonly KingdeeApiClient? _kingdee;
    private readonly ILogger<AdminController> _logger;

    public AdminController(AppConfigStore config, KingdeeApiClient? kingdee, ILogger<AdminController> logger)
    {
        _config = config;
        _kingdee = kingdee;
        _logger = logger;
    }

    /// <summary>读取当前配置（不需要登录，密码字段会被脱敏）</summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var cfg = _config.Get();
        return Ok(new
        {
            Kingdee = new
            {
                cfg.KingdeeEnable,
                cfg.KingdeeServerUrl,
                cfg.KingdeeDbId,
                cfg.KingdeeUserName,
                Password = string.IsNullOrWhiteSpace(cfg.KingdeePassword) ? "" : "******",
                cfg.KingdeeLcId,
                cfg.KingdeeTimeoutSeconds,
                cfg.KingdeeImageServerUrl,
                cfg.KingdeeMaterialFormId
            },
            FieldMappings = cfg.FieldMappings,
            WeCom = new
            {
                cfg.WeComEnable,
                cfg.WeComCorpId,
                cfg.WeComAgentId,
                Secret = string.IsNullOrWhiteSpace(cfg.WeComSecret) ? "" : "******",
                cfg.WeComCallbackUrl,
                JwtSecret = string.IsNullOrWhiteSpace(cfg.WeComJwtSecret) ? "" : "******",
                cfg.WeComJwtExpireHours,
                cfg.WeComForceLogin
            },
            AdminPassword = string.IsNullOrWhiteSpace(cfg.AdminPassword) ? "" : "******",
            FilePath = _config.FilePath
        });
    }

    /// <summary>保存金蝶连接配置（需要管理员登录）</summary>
    [Authorize(AuthenticationSchemes = "AdminJwt")]
    [HttpPost("config/kingdee")]
    public IActionResult SaveKingdee([FromBody] KingdeeConfigPayload model)
    {
        var cfg = _config.Get();
        cfg.KingdeeEnable = model.Enable;
        cfg.KingdeeServerUrl = model.ServerUrl?.Trim() ?? "";
        cfg.KingdeeDbId = model.DbId?.Trim() ?? "";
        cfg.KingdeeUserName = model.UserName?.Trim() ?? "";
        // 只有传了非 "******" 的新值才更新密码
        if (!string.IsNullOrWhiteSpace(model.Password) && model.Password != "******")
        {
            cfg.KingdeePassword = model.Password;
        }
        cfg.KingdeeLcId = model.LcId;
        cfg.KingdeeTimeoutSeconds = model.TimeoutSeconds;
        cfg.KingdeeImageServerUrl = model.ImageServerUrl?.Trim() ?? "";
        cfg.KingdeeMaterialFormId = model.MaterialFormId?.Trim() ?? "BD_MATERIAL";
        _config.Save(cfg);
        return Ok(new { success = true, msg = "金蝶配置已保存" });
    }

    /// <summary>保存字段映射</summary>
    [Authorize(AuthenticationSchemes = "AdminJwt")]
    [HttpPost("config/field-mappings")]
    public IActionResult SaveFieldMappings([FromBody] List<FieldMappingItem> model)
    {
        if (model == null || model.Count == 0)
            return BadRequest(new { success = false, msg = "字段映射不能为空" });

        var cfg = _config.Get();
        cfg.FieldMappings = model;
        _config.Save(cfg);
        return Ok(new { success = true, msg = $"已保存 {model.Count} 条字段映射" });
    }

    /// <summary>保存企业微信配置</summary>
    [Authorize(AuthenticationSchemes = "AdminJwt")]
    [HttpPost("config/wecom")]
    public IActionResult SaveWeCom([FromBody] WeComConfigPayload model)
    {
        var cfg = _config.Get();
        cfg.WeComEnable = model.Enable;
        cfg.WeComCorpId = model.CorpId?.Trim() ?? "";
        cfg.WeComAgentId = model.AgentId?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(model.Secret) && model.Secret != "******")
            cfg.WeComSecret = model.Secret;
        cfg.WeComCallbackUrl = model.CallbackUrl?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(model.JwtSecret) && model.JwtSecret != "******")
            cfg.WeComJwtSecret = model.JwtSecret;
        cfg.WeComJwtExpireHours = model.JwtExpireHours;
        cfg.WeComForceLogin = model.ForceLogin;
        _config.Save(cfg);
        return Ok(new { success = true, msg = "企业微信配置已保存" });
    }

    /// <summary>修改管理员密码</summary>
    [Authorize(AuthenticationSchemes = "AdminJwt")]
    [HttpPost("config/admin-password")]
    public IActionResult SaveAdminPassword([FromBody] AdminPasswordPayload model)
    {
        if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
            return BadRequest(new { success = false, msg = "密码至少 6 位" });

        var cfg = _config.Get();
        cfg.AdminPassword = model.NewPassword;
        _config.Save(cfg);
        return Ok(new { success = true, msg = "管理员密码已更新" });
    }

    /// <summary>管理员登录（POST，body = { password }）</summary>
    [HttpPost("auth/login")]
    public IActionResult Login([FromBody] AdminLoginPayload model)
    {
        var cfg = _config.Get();
        if (string.IsNullOrWhiteSpace(model.Password))
            return Unauthorized(new { success = false, msg = "请输入密码" });
        if (model.Password != cfg.AdminPassword)
            return Unauthorized(new { success = false, msg = "密码错误" });

        var token = IssueAdminJwt(cfg);
        return Ok(new { success = true, token, expireHours = 12 });
    }

    /// <summary>测试金蝶连接</summary>
    [Authorize(AuthenticationSchemes = "AdminJwt")]
    [HttpPost("test/kingdee")]
    public async Task<IActionResult> TestKingdee([FromBody] KingdeeConfigPayload model)
    {
        if (_kingdee == null) return StatusCode(500, new { success = false, msg = "金蝶客户端未注册" });

        try
        {
            var ok = await _kingdee.LoginRawAsync(
                model.ServerUrl?.Trim() ?? "",
                model.DbId?.Trim() ?? "",
                model.UserName?.Trim() ?? "",
                model.Password == "******" ? _config.Get().KingdeePassword : model.Password ?? "",
                model.LcId,
                model.TimeoutSeconds);

            return Ok(new { success = ok, msg = ok ? "登录成功，金蝶 API 可用" : "登录失败，请检查地址/账套/账号密码" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试金蝶连接异常");
            return Ok(new { success = false, msg = "异常：" + ex.Message });
        }
    }

    /// <summary>测试企业微信</summary>
    [Authorize(AuthenticationSchemes = "AdminJwt")]
    [HttpPost("test/wecom")]
    public async Task<IActionResult> TestWeCom([FromBody] WeComConfigPayload model)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10), BaseAddress = new Uri("https://qyapi.weixin.qq.com") };
            var secret = model.Secret == "******" ? _config.Get().WeComSecret : model.Secret ?? "";
            var url = $"/cgi-bin/gettoken?corpid={Uri.EscapeDataString(model.CorpId ?? "")}&corpsecret={Uri.EscapeDataString(secret)}";
            var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return Ok(new { success = false, msg = $"HTTP {(int)resp.StatusCode}" });

            var body = await resp.Content.ReadAsStringAsync();
            var data = System.Text.Json.JsonDocument.Parse(body).RootElement;
            var errCode = data.GetProperty("errcode").GetInt32();
            if (errCode != 0)
                return Ok(new { success = false, msg = $"企业微信返回错误 {errCode}：{data.GetProperty("errmsg").GetString()}" });

            return Ok(new { success = true, msg = "企业微信 API 可用（已获取 access_token）" });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, msg = "异常：" + ex.Message });
        }
    }

    private static string IssueAdminJwt(AppConfig cfg)
    {
        var key = Encoding.ASCII.GetBytes(
            string.IsNullOrWhiteSpace(cfg.WeComJwtSecret) ? "default-admin-secret-please-change-this" : cfg.WeComJwtSecret);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Admin") }),
            Expires = DateTime.UtcNow.AddHours(12),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Issuer = "Kingdee.MaterialAPI",
            Audience = "AdminUser"
        };
        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}

public class KingdeeConfigPayload
{
    public bool Enable { get; set; }
    public string? ServerUrl { get; set; }
    public string? DbId { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public int LcId { get; set; } = 2052;
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
    public bool ForceLogin { get; set; } = true;
}

public class AdminLoginPayload { public string? Password { get; set; } }

public class AdminPasswordPayload { public string? NewPassword { get; set; } }

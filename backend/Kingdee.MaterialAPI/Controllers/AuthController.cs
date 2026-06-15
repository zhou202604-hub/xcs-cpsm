using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kingdee.MaterialAPI.Controllers;

/// <summary>
/// 企业微信认证相关接口
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly WeComAuthService _weCom;
    private readonly WeComSettings _settings;
    private readonly ILogger<AuthController> _logger;

    public AuthController(WeComAuthService weCom, IOptions<WeComSettings> settings, ILogger<AuthController> logger)
    {
        _weCom = weCom;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// 企业微信 OAuth2 回调地址
    /// URL: https://material.your-company.com/api/auth?code=CODE&state=STATE
    /// </summary>
    [HttpGet]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Auth(
        [FromQuery] string? code = null,
        [FromQuery] string? state = null,
        CancellationToken ct = default)
    {
        // 未配置企业微信：允许匿名访问，返回 dev 模式的伪 token
        if (!_weCom.IsConfigured)
        {
            var devToken = _weCom.IssueJwt("dev-user", "开发调试用户");
            var html = BuildSetTokenHtml(devToken, "开发模式", "/");
            return Content(html, "text/html; charset=utf-8");
        }

        // 没有 code：跳转去企业微信授权页
        if (string.IsNullOrWhiteSpace(code))
        {
            var redirect = _weCom.BuildOAuthUrl(state);
            return Redirect(redirect);
        }

        // 有 code：走正常流程
        var user = await _weCom.ExchangeCodeAsync(code, ct);
        if (user == null)
        {
            return Content(@"<html><body><h3>企业微信认证失败</h3><p>请关闭页面后从企业微信应用重新进入。</p></body></html>",
                "text/html; charset=utf-8");
        }

        // 拉取姓名
        var info = await _weCom.GetUserInfoAsync(user.Value.UserId, ct);
        var name = info?.Name ?? user.Value.UserId;

        var token = _weCom.IssueJwt(user.Value.UserId, name);

        // state 可能是 base64 后的跳转路径
        var redirectPath = "/";
        if (!string.IsNullOrWhiteSpace(state) && state != "home")
        {
            try
            {
                var bytes = Convert.FromBase64String(state);
                redirectPath = System.Text.Encoding.UTF8.GetString(bytes);
                if (string.IsNullOrWhiteSpace(redirectPath) || !redirectPath.StartsWith('/'))
                    redirectPath = "/";
            }
            catch
            {
                redirectPath = "/";
            }
        }

        _logger.LogInformation("企业微信登录成功: {UserId} ({Name})", user.Value.UserId, name);

        // 返回一个自动写 token 并跳回首页的 HTML
        var html2 = BuildSetTokenHtml(token, name, redirectPath);
        return Content(html2, "text/html; charset=utf-8");
    }

    /// <summary>
    /// 获取当前登录人（JWT 里读 userid / name）
    /// </summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(token)) token = Request.Cookies["wct"];

        if (string.IsNullOrWhiteSpace(token))
        {
            return Ok(new { Authenticated = false, Mode = _weCom.IsConfigured ? "WeCom" : "Dev" });
        }

        var (ok, userId, name) = _weCom.ValidateJwt(token);
        return Ok(new
        {
            Authenticated = ok,
            UserId = userId,
            Name = name,
            Mode = _weCom.IsConfigured ? "WeCom" : "Dev"
        });
    }

    /// <summary>
    /// 给前端返回企业微信 JS-SDK 所需的 signature 等参数
    /// </summary>
    [HttpGet("jssdk")]
    public async Task<IActionResult> JsSdk([FromQuery] string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) url = Request.Headers["Referer"].FirstOrDefault() ?? "";
        var (appId, timestamp, nonce, signature, errCode, errMsg) = await _weCom.BuildJsSdkSignAsync(url, ct);
        return Ok(new
        {
            appId,
            timestamp,
            nonceStr = nonce,
            signature,
            errCode,
            errMsg,
            debug = false,
            jsApiList = new[] { "scanQRCode" }
        });
    }

    /// <summary>
    /// 简单的健康检查 / 配置状态返回
    /// </summary>
    [HttpGet("config")]
    public IActionResult Config()
    {
        return Ok(new
        {
            WeComConfigured = _weCom.IsConfigured,
            CorpId = _settings.CorpId,
            AgentId = _settings.AgentId,
            CallbackUrl = _settings.CallbackUrl,
            ForceLogin = _settings.ForceWeComLogin
        });
    }

    private static string BuildSetTokenHtml(string token, string name, string redirectPath)
    {
        var safeToken = token.Replace("\"", "\\\"");
        var safeName = System.Net.WebUtility.HtmlEncode(name);
        var safePath = redirectPath;
        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""utf-8"" />
<title>登录中 - 物料查询</title>
<meta name=""viewport"" content=""width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no"" />
<style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'PingFang SC', sans-serif;
           display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;
           background:#f5f7fa;color:#262626; }}
    .card {{ background:#fff;padding:32px 24px;border-radius:10px;text-align:center;
             box-shadow:0 2px 8px rgba(0,0,0,.08); }}
    h2 {{ font-size:18px;margin:0 0 8px;color:#1890ff; }}
    p {{ font-size:13px;color:#8c8c8c;margin:0; }}
    .spinner {{ width:32px;height:32px;border:3px solid #eee;border-top-color:#1890ff;
                border-radius:50%;animation:spin .8s linear infinite;margin:0 auto 16px; }}
    @keyframes spin {{ to {{ transform: rotate(360deg); }} }}
</style>
</head>
<body>
<div class=""card"">
    <div class=""spinner""></div>
    <h2>欢迎，{safeName}</h2>
    <p>正在进入物料查询系统…</p>
</div>
<script>
(function(){{
    try {{
        localStorage.setItem('wc_token', '{safeToken}');
        localStorage.setItem('wc_name', '{safeName}');
    }} catch(e) {{}}
    setTimeout(function(){{ window.location.replace('{safePath}'); }}, 400);
}})();
</script>
</body>
</html>";
    }
}

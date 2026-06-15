using Kingdee.MaterialAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kingdee.MaterialAPI.Controllers;

/// <summary>
/// 企业微信 OAuth2 回调：
///   GET /api/auth           → 无 code → 跳企业微信登录 / 回首页
///   GET /api/auth?code=xxx  → 交换 code → 发 token → 跳回首页
///   GET /api/auth/me        → 当前登录人
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly WeComAuthService _weCom;
    private readonly ILogger<AuthController> _logger;

    public AuthController(WeComAuthService weCom, ILogger<AuthController> logger)
    {
        _weCom = weCom;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Auth([FromQuery] string? code = null)
    {
        // 未配置企业微信：返回简单的匿名登录页（直接用内置用户"dev-user"）
        if (!_weCom.IsWeComConfigured)
        {
            var fakeToken = _weCom.IssueToken("dev-user", "开发调试用户");
            return Content(BuildSetTokenHtml(fakeToken, "开发调试用户"), "text/html; charset=utf-8");
        }

        // 无 code → 跳企业微信授权
        if (string.IsNullOrWhiteSpace(code))
        {
            return Redirect(_weCom.BuildOAuthUrl());
        }

        // 有 code → 拿 userid / name → 签发 token → 写 localStorage + 回首页
        var (uid, name) = await _weCom.ExchangeCodeGetUser(code);
        if (string.IsNullOrWhiteSpace(uid))
        {
            return Content(@"<html><body><h3>企业微信登录失败</h3><p>请关闭页面后重新打开应用</p></body></html>",
                "text/html; charset=utf-8");
        }

        var token = _weCom.IssueToken(uid, name);
        _logger.LogInformation("企业微信登录：{User} ({Name})", uid, name);
        return Content(BuildSetTokenHtml(token, name), "text/html; charset=utf-8");
    }

    [HttpGet("me")]
    public IActionResult Me([FromHeader(Name = "Authorization")] string? authHeader = null)
    {
        string? token = null;
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = authHeader.Substring(7).Trim();
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            token = Request.Query["token"];
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            return Ok(new { authenticated = false });
        }

        var (ok, userId, name) = _weCom.ValidateToken(token);
        return Ok(new
        {
            authenticated = ok,
            userId = ok ? userId : "",
            name = ok ? name : "",
            weComEnabled = _weCom.IsWeComConfigured
        });
    }

    private static string BuildSetTokenHtml(string token, string name)
    {
        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""utf-8"" />
<title>登录中 - 物料查询</title>
<meta name=""viewport"" content=""width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no"" />
</head>
<body style=""display:flex;align-items:center;justify-content:center;min-height:100vh;background:#f5f7fa;font-family:-apple-system,BlinkMacSystemFont,'PingFang SC',sans-serif;"">
<div style=""background:#fff;padding:24px 28px;border-radius:10px;text-align:center;box-shadow:0 2px 8px rgba(0,0,0,.08);"">
<h2 style=""font-size:18px;margin:0 0 8px;color:#1890ff;"">欢迎，{System.Net.WebUtility.HtmlEncode(name)}</h2>
<p style=""color:#8c8c8c;font-size:13px;margin:0;"">正在进入物料查询系统…</p>
</div>
<script>
(function(){{
try {{ localStorage.setItem('wc_token','{token.Replace("'","\\'").Replace("\\","\\\\")}'); }} catch(e){{}}
setTimeout(function(){{ window.location.replace('/'); }}, 400);
}})();
</script>
</body>
</html>";
    }
}

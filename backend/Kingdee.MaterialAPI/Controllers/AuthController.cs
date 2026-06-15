using Kingdee.MaterialAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace Kingdee.MaterialAPI.Controllers;

/// <summary>
/// 企业微信回调 &amp; 当前用户 &amp; JS-SDK 签名
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly WeComAuthService _weCom;
    private readonly AppConfigStore _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(WeComAuthService weCom, AppConfigStore config, ILogger<AuthController> logger)
    {
        _weCom = weCom;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// 企业微信回调 URL（在企业微信管理后台配置为可信回调）
    /// 无 code 时自动跳转到企业微信授权页；带 code 时拿 userid → 发 JWT → 写 cookie + localStorage → 返回首页
    /// </summary>
    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Callback([FromQuery] string? code = null,
        [FromQuery] string? state = null,
        CancellationToken ct = default)
    {
        // 未配置企业微信 → 返回登录页（开发调试）
        if (!_weCom.IsConfigured)
        {
            var devToken = _weCom.IssueJwt("dev-user", "开发调试用户");
            return Content(BuildSetTokenHtml(devToken, "开发调试用户", "/"),
                "text/html; charset=utf-8");
        }

        // 没 code → 跳转企业微信授权
        if (string.IsNullOrWhiteSpace(code))
        {
            return Redirect(_weCom.BuildOAuthUrl(state));
        }

        // 用 code 换 userid
        var userid = await _weCom.ExchangeCodeAsync(code, ct);
        if (string.IsNullOrWhiteSpace(userid))
        {
            return Content(@"<html><body><h3>企业微信登录失败</h3><p>请回到企业微信，重新打开应用</p></body></html>",
                "text/html; charset=utf-8");
        }

        var name = await _weCom.GetUserNameAsync(userid, ct);
        var token = _weCom.IssueJwt(userid, name);

        // state 里携带的原始跳转路径
        var redirectPath = "/";
        if (!string.IsNullOrWhiteSpace(state) && state != "home")
        {
            try
            {
                var bytes = Convert.FromBase64String(state);
                var decoded = System.Text.Encoding.UTF8.GetString(bytes);
                if (!string.IsNullOrWhiteSpace(decoded) && decoded.StartsWith('/'))
                    redirectPath = decoded;
            }
            catch { }
        }

        _logger.LogInformation("企业微信登录: {UserId}({Name})", userid, name);
        return Content(BuildSetTokenHtml(token, name, redirectPath), "text/html; charset=utf-8");
    }

    /// <summary>返回当前用户（前端校验 JWT）</summary>
    [HttpGet("me")]
    public IActionResult Me()
    {
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(token)) token = Request.Cookies["wct"];
        if (string.IsNullOrWhiteSpace(token))
            return Ok(new { Authenticated = false, Mode = _weCom.IsConfigured ? "WeCom" : "Dev" });

        var (ok, uid, name) = _weCom.ValidateJwt(token);
        return Ok(new { Authenticated = ok, UserId = uid, Name = name, Mode = _weCom.IsConfigured ? "WeCom" : "Dev" });
    }

    /// <summary>JS-SDK 签名（将来如需扫码可以加上）</summary>
    [HttpGet("jssdk")]
    public async Task<IActionResult> JsSdk([FromQuery] string url, CancellationToken ct = default)
    {
        var (appId, timestamp, nonce, sig, err, errMsg) =
            await _weCom.BuildJsSdkSignAsync(string.IsNullOrWhiteSpace(url) ? Request.Headers["Referer"].FirstOrDefault() ?? "" : url, ct);
        return Ok(new
        {
            appId,
            timestamp,
            nonceStr = nonce,
            signature = sig,
            errCode = err,
            errMsg,
            debug = false
        });
    }

    [HttpGet("config")]
    public IActionResult ConfigStatus()
    {
        var c = _config.Get();
        return Ok(new
        {
            WeComConfigured = _weCom.IsConfigured,
            CorpId = c.WeComCorpId,
            AgentId = c.WeComAgentId,
            CallbackUrl = c.WeComCallbackUrl
        });
    }

    private static string BuildSetTokenHtml(string token, string name, string redirectPath)
    {
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
    p {{ font-size:12px;color:#8c8c8c;margin:0; }}
</style>
</head>
<body>
<div class=""card"">
    <h2>欢迎，{System.Net.WebUtility.HtmlEncode(name)}</h2>
    <p>正在进入物料查询系统…</p>
</div>
<script>
(function(){{
    try {{
        localStorage.setItem('wc_token', '{token.Replace("\"", "\\\"")}');
        localStorage.setItem('wc_name', '{System.Net.WebUtility.HtmlEncode(name).Replace("'", "\\'")}');
    }} catch(e) {{}}
    setTimeout(function(){{ window.location.replace('{redirectPath}'); }}, 300);
}})();
</script>
</body>
</html>";
    }
}

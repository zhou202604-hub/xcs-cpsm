using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Kingdee.MaterialAPI.Models;

namespace Kingdee.MaterialAPI.Services;

/// <summary>
/// 金蝶云星空 Web API 客户端
/// 
/// 官方通用协议（BOS WebApi ServicesStub）：
///   POST http://{server}/Kingdee.BOS.WebApi.ServicesStub.{ServiceName}.{Method}.common.kdsvc
///   Content-Type: application/json; charset=utf-8
///   Body:
///     { "format": 1, "useragent": "MyApp", "parameters": [...] }
///   返回:
///     { "LoginResultType": 1 }  // 登录
///     [...]                      // executeBillQuery（二维数组）
///     { "Result": {...} }       // View / Save
///
/// 本类提供以下方法：
///   LoginAsync()                  → 调用 AuthService.ValidateUser
///   ExecuteBillQueryAsync(sql)   → 调用 DynamicFormService.ExecuteBillQuery
///   ViewAsync(formId, pkValue)   → 调用 DynamicFormService.View（用于获取附件）
/// </summary>
public class KingdeeApiClient
{
    private readonly AppConfigStore _configStore;
    private readonly ILogger<KingdeeApiClient> _logger;
    private readonly HttpClient _http;

    // 登录 Cookie 的内存缓存（key: server|dbid|username, value: cookies 字符串）
    private static readonly ConcurrentDictionary<string, (string Cookies, DateTime ExpireAt)> _loginCache = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public KingdeeApiClient(AppConfigStore configStore, ILogger<KingdeeApiClient> logger)
    {
        _configStore = configStore;
        _logger = logger;
        _http = new HttpClient(new HttpClientHandler
        {
            UseCookies = true,
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
        _http.DefaultRequestHeaders.Add("User-Agent", "Kingdee.MaterialAPI");
    }

    // ========= 公开方法 =========
    /// <summary>测试登录 —— 不带缓存，直接请求金蝶</summary>
    public async Task<(bool Ok, string Msg)> TestLoginAsync(string serverUrl, string dbId, string userName, string password, int lcId)
    {
        try
        {
            var ok = await DoLoginAsync(serverUrl, dbId, userName, password, lcId);
            return (ok, ok ? "登录成功" : "登录失败，请检查地址/账套/用户名/密码");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "测试金蝶登录异常");
            return (false, "异常：" + ex.Message);
        }
    }

    /// <summary>执行 BillQuery，返回二维列表（金蝶原始结构）</summary>
    public async Task<List<object[]>?> ExecuteBillQueryAsync(string selectFields, string filter, string orderBy, int top)
    {
        var cfg = _configStore.Get().Kingdee;
        if (!cfg.Enable) return null;
        if (string.IsNullOrWhiteSpace(cfg.ServerUrl) || string.IsNullOrWhiteSpace(cfg.DbId)) return null;

        var cookies = await GetCookiesAsync(cfg);
        if (cookies == null) return null;

        var body = new
        {
            format = 1,
            useragent = "Kingdee.MaterialAPI",
            parameters = new object[]
            {
                cfg.MaterialFormId,          // formId
                "",                           // 组织 OrgId（可选）
                0,                            // PageIndex
                top,                          // PageSize / TopRowCount
                0,                            // GroupCount
                0,                            // RecordCount
                "",                           // ParentInteractId
                selectFields,                 // 字段列表（逗号分隔）
                filter,                       // WHERE 子句（不含 WHERE 关键字）
                orderBy,                      // ORDER BY 子句（不含 ORDER BY）
                ""                            // FilterString
            }
        };

        var json = await PostJsonAsync(
            BuildUrl(cfg.ServerUrl, "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService", "ExecuteBillQuery"),
            body, cookies);
        if (json == null) return null;

        try
        {
            // 处理多种返回形状：数组 / { Result: [...] } / { Data: [...] } / 单个对象
            using var doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("Result", out var r)) root = r;
            else if (root.TryGetProperty("Data", out var d)) root = d;
            else if (root.TryGetProperty("data", out var d2)) root = d2;

            if (root.ValueKind != JsonValueKind.Array) return new List<object[]>();

            var list = new List<object[]>();
            foreach (var row in root.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Array)
                {
                    var cells = row.EnumerateArray().Select(el => JsonElementToObject(el)).ToArray();
                    list.Add(cells!);
                }
            }
            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析金蝶查询返回失败: {Sample}", json.Length > 200 ? json.Substring(0, 200) : json);
            return null;
        }
    }

    /// <summary>
    /// View —— 取单据详情（用于解析图片/附件）
    /// </summary>
    public async Task<JsonDocument?> ViewAsync(string pkValue)
    {
        var cfg = _configStore.Get().Kingdee;
        if (!cfg.Enable) return null;
        var cookies = await GetCookiesAsync(cfg);
        if (cookies == null) return null;

        var body = new
        {
            format = 1,
            useragent = "Kingdee.MaterialAPI",
            parameters = new object[]
            {
                cfg.MaterialFormId,
                new { PKId = pkValue, CreateOrg = 0, UseOrg = 0 }
            }
        };
        var json = await PostJsonAsync(
            BuildUrl(cfg.ServerUrl, "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService", "View"),
            body, cookies);
        if (json == null) return null;
        try
        {
            return JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }
    }

    // ========= 内部方法 =========
    private async Task<string?> GetCookiesAsync(KingdeeSettings cfg)
    {
        var key = $"{cfg.ServerUrl}|{cfg.DbId}|{cfg.UserName}";
        if (_loginCache.TryGetValue(key, out var cached) && cached.ExpireAt > DateTime.Now)
            return cached.Cookies;

        var ok = await DoLoginAsync(cfg.ServerUrl, cfg.DbId, cfg.UserName, cfg.Password, cfg.LcId);
        if (!ok) return null;
        if (_loginCache.TryGetValue(key, out var d)) return d.Cookies;
        return null;
    }

    private async Task<bool> DoLoginAsync(string serverUrl, string dbId, string userName, string password, int lcId)
    {
        try
        {
            var key = $"{serverUrl}|{dbId}|{userName}";
            var body = new
            {
                format = 1,
                useragent = "Kingdee.MaterialAPI",
                parameters = new object[] { dbId, userName, password, lcId, "" }
            };
            var url = BuildUrl(serverUrl, "Kingdee.BOS.WebApi.ServicesStub.AuthService", "ValidateUser");
            _logger.LogInformation("登录金蝶: {Url}", url);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("金蝶登录返回: {Json}", json.Length > 256 ? json.Substring(0, 256) : json);

            // 解析 LoginResultType
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int loginResult = -1;
            if (root.TryGetProperty("LoginResultType", out var t))
            {
                if (t.ValueKind == JsonValueKind.Number) loginResult = t.GetInt32();
                else if (t.ValueKind == JsonValueKind.True) loginResult = 1;
            }
            else if (root.TryGetProperty("ResultType", out var t2))
            {
                if (t2.ValueKind == JsonValueKind.Number) loginResult = t2.GetInt32();
            }

            // 收集响应 Cookie
            var cookies = new StringBuilder();
            if (resp.Headers.TryGetValues("Set-Cookie", out var cookiesRaw))
            {
                foreach (var c in cookiesRaw)
                {
                    // 只取 name=value 部分
                    var part = c.Split(';')[0].Trim();
                    if (cookies.Length > 0) cookies.Append(';');
                    cookies.Append(part);
                }
            }
            if (loginResult == 1)
            {
                _loginCache[key] = (cookies.ToString(), DateTime.Now.AddMinutes(60));
                return true;
            }
            _logger.LogWarning("金蝶登录返回非成功值: {Json}", json);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "金蝶登录异常");
            return false;
        }
    }

    private async Task<string?> PostJsonAsync(string url, object body, string? cookies)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrWhiteSpace(cookies))
                req.Headers.Add("Cookie", cookies);
            req.Content = new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "金蝶请求失败: {Url}", url);
            return null;
        }
    }

    private static string BuildUrl(string serverUrl, string service, string method)
    {
        var baseUrl = (serverUrl ?? "").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) return "";
        return $"{baseUrl}/{service}.{method}.common.kdsvc";
    }

    private static object? JsonElementToObject(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };
    }
}

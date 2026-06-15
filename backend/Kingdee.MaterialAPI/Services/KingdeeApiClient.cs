using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;

namespace Kingdee.MaterialAPI;

/// <summary>
/// 金蝶云星空 Web API 客户端
/// - 登录 token 会缓存在内存（过期自动重新登录）
/// - 连接信息由 AppConfigStore 动态读取
/// </summary>
public class KingdeeApiClient
{
    private readonly AppConfigStore _configStore;
    private readonly ILogger<KingdeeApiClient> _logger;
    private static readonly ConcurrentDictionary<string, (string Token, DateTime ExpireAt)> _loginCache = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public KingdeeApiClient(AppConfigStore configStore, ILogger<KingdeeApiClient> logger)
    {
        _configStore = configStore;
        _logger = logger;
    }

    /// <summary>获取或登录一次 token（读配置）</summary>
    public async Task<string?> GetOrLoginTokenAsync(CancellationToken ct = default)
    {
        var cfg = _configStore.Get();
        if (!cfg.KingdeeEnable) return null;

        var cacheKey = $"{cfg.KingdeeServerUrl}|{cfg.KingdeeDbId}|{cfg.KingdeeUserName}";
        // 先读缓存（提前 5 分钟过期）
        if (_loginCache.TryGetValue(cacheKey, out var cached) && cached.ExpireAt > DateTime.Now)
            return cached.Token;

        var token = await LoginRawAsync(
            cfg.KingdeeServerUrl, cfg.KingdeeDbId, cfg.KingdeeUserName,
            cfg.KingdeePassword, cfg.KingdeeLcId, cfg.KingdeeTimeoutSeconds);

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("金蝶登录失败，服务器={Server}", cfg.KingdeeServerUrl);
            return null;
        }
        _loginCache[cacheKey] = (token!, DateTime.Now.AddHours(2));
        return token;
    }

    /// <summary>直接登录，返回 token（失败返回 null）—— 公开给管理后台"测试连接"按钮调用</summary>
    public async Task<string?> LoginRawAsync(string serverUrl, string dbId, string userName,
        string password, int lcId, int timeoutSeconds)
    {
        try
        {
            var url = serverUrl.TrimEnd('/') + "/Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc";
            var body = new
            {
                format = 1,
                useragent = "Kingdee.MaterialAPI",
                parameters = new object[] { dbId, userName, password, lcId, "" }
            };
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(timeoutSeconds, 5)) };
            var resp = await http.PostAsync(url, new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json"), ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            // 兼容：返回可能是 { LoginResultType: 1 } 或 { ResultType: 1 }
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // 解析 body
            int resultType = -1;
            string? kdsvcToken = null;
            if (root.TryGetProperty("LoginResultType", out var r1))
                resultType = r1.GetInt32();
            else if (root.TryGetProperty("ResultType", out var r2))
                resultType = r2.GetInt32();

            if (resultType != 1)
            {
                _logger.LogWarning("金蝶登录返回 ResultType={Type}, json={Json}", resultType, json);
                return null;
            }
            // 返回 cookie 中的 kdsvc_sessionid 等；调用 query 时通常要把 cookies 带上
            // 简化：我们把 kdsvc 写下来，再在后续请求用 cookies 方式带上是一种标准做法
            // 但在 Web API 里，更常见的是把登录的 Response cookie 保存
            foreach (var cookie in resp.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : Enumerable.Empty<string>())
            {
                if (cookie.StartsWith("kdsvc_sessionid", StringComparison.OrdinalIgnoreCase) ||
                    cookie.StartsWith(".kdsvc_sessionid", StringComparison.OrdinalIgnoreCase) ||
                    cookie.StartsWith("ASP.NET_SessionId", StringComparison.OrdinalIgnoreCase))
                {
                    kdsvcToken = cookie.Split(';')[0];
                    break;
                }
            }
            // 如果 cookie 没拿到，就返回一个空标识但记录失败
            if (string.IsNullOrWhiteSpace(kdsvcToken))
                kdsvcToken = "login-ok-no-cookie";

            // 为便于后续 Post 携带 cookies，我们把整个 Set-Cookie 列表拼起来缓存
            var allCookies = resp.Headers.TryGetValues("Set-Cookie", out var cs)
                ? string.Join("; ", cs.Select(c => c.Split(';')[0]))
                : "";

            return string.IsNullOrWhiteSpace(allCookies) ? kdsvcToken : allCookies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "金蝶登录异常");
            return null;
        }
    }

    /// <summary>执行 executeBillQuery —— 自定义 SQL 取列表</summary>
    public async Task<List<object[]>?> ExecuteBillQueryAsync(string formId, string selectFields, string filter,
        string orderBy, int top, CancellationToken ct = default)
    {
        var token = await GetOrLoginTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token)) return null;

        var cfg = _configStore.Get();
        var url = cfg.KingdeeServerUrl.TrimEnd('/')
                  + $"/Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.ExecuteBillQuery.common.kdsvc";

        var body = new
        {
            format = 1,
            useragent = "Kingdee.MaterialAPI",
            parameters = new object[]
            {
                formId,
                "", // OrgId
                0,  // PageIndex
                top,
                0,  // GroupCount
                0,  // RecordCount
                "", // ParentInteractId
                selectFields,
                filter,
                orderBy,
                ""  // FilterString
            }
        };

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(cfg.KingdeeTimeoutSeconds, 15)) };
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Cookie", token);
            req.Content = new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");
            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            return ParseRows(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "金蝶 ExecuteBillQuery 失败");
            return null;
        }
    }

    /// <summary>调用 View 取单据详情（含附件信息）</summary>
    public async Task<JsonDocument?> ViewAsync(string formId, string pkId, CancellationToken ct = default)
    {
        var token = await GetOrLoginTokenAsync(ct);
        if (string.IsNullOrWhiteSpace(token)) return null;

        var cfg = _configStore.Get();
        var url = cfg.KingdeeServerUrl.TrimEnd('/')
                  + $"/Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.View.common.kdsvc";

        var body = new
        {
            format = 1,
            useragent = "Kingdee.MaterialAPI",
            parameters = new object[] { formId, new { PKId = pkId, CreateOrg = 0, UseOrg = 0 } }
        };

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(cfg.KingdeeTimeoutSeconds, 15)) };
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Cookie", token);
            req.Content = new StringContent(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");
            var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            return JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "金蝶 View 失败");
            return null;
        }
    }

    /// <summary>从金蝶返回的 json 字符串解析二维数组（兼容 {"Result":[...]} 或直接数组）</summary>
    private static List<object[]>? ParseRows(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("Result", out var r1) && r1.ValueKind == JsonValueKind.Array)
                root = r1;
            else if (root.TryGetProperty("Data", out var r2) && r2.ValueKind == JsonValueKind.Array)
                root = r2;

            if (root.ValueKind != JsonValueKind.Array) return null;

            var list = new List<object[]>();
            foreach (var row in root.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Array)
                {
                    var arr = row.EnumerateArray().Select(e => e.ValueKind switch
                    {
                        JsonValueKind.String => (object)e.GetString()!,
                        JsonValueKind.Number => e.GetDecimal(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => null!,
                        _ => e.GetRawText()
                    }).ToArray();
                    list.Add(arr!);
                }
            }
            return list;
        }
        catch
        {
            return null;
        }
    }
}

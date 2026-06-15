using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kingdee.MaterialAPI.Models;
using Microsoft.Extensions.Options;

namespace Kingdee.MaterialAPI.Services;

/// <summary>
/// 金蝶云星空 Web API 客户端
/// 参考官方文档的调用方式：登录 -> 业务查询
/// </summary>
public class KingdeeApiClient
{
    private readonly KingdeeSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private readonly ILogger<KingdeeApiClient> _logger;

    // 登录会话缓存：一次登录后可复用 Cookie
    private DateTime _loginAt = DateTime.MinValue;
    private readonly object _loginLock = new();
    private bool _isLoggedIn = false;

    public KingdeeApiClient(IOptions<KingdeeSettings> settings, ILogger<KingdeeApiClient> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        _cookieContainer = new CookieContainer();
        var handler = new SocketsHttpHandler
        {
            CookieContainer = _cookieContainer,
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };
        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds)
        };
    }

    private string NormalizeServerUrl()
    {
        var url = _settings.ServerUrl.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }
        if (!url.EndsWith("/")) url += "/";
        return url;
    }

    private string ImageServer()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ImageServerUrl))
        {
            var u = _settings.ImageServerUrl.Trim();
            if (!u.EndsWith("/")) u += "/";
            return u;
        }
        return NormalizeServerUrl();
    }

    /// <summary>
    /// 登录金蝶云星空
    /// 接口：Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc
    /// </summary>
    public async Task<bool> LoginAsync(CancellationToken cancellationToken = default)
    {
        // 简单的会话重用：两小时内不再重新登录
        lock (_loginLock)
        {
            if (_isLoggedIn && (DateTime.Now - _loginAt).TotalHours < 2)
            {
                return true;
            }
        }

        var serverUrl = NormalizeServerUrl();
        var loginUrl = serverUrl + "Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc";

        // 登录参数：[dbId, username, password, lcId]
        var @params = new JsonArray
        {
            _settings.DbId,
            _settings.UserName,
            _settings.Password,
            _settings.LcId
        };

        var body = BuildRequestPayload(@params);
        _logger.LogInformation("登录金蝶云星空 {Url}", loginUrl);

        try
        {
            var response = await SendPostAsync(loginUrl, body, cancellationToken);
            var json = JsonNode.Parse(response);
            // 标准返回：{ "Result": { "ResponseStatus": { "IsSuccess": true/false, ... } } }
            var isSuccess = json?["Result"]?["ResponseStatus"]?["IsSuccess"]?.GetValue<bool>()
                            ?? json?["IsSuccess"]?.GetValue<bool>()
                            ?? false;

            if (isSuccess)
            {
                lock (_loginLock) { _isLoggedIn = true; _loginAt = DateTime.Now; }
                _logger.LogInformation("金蝶云星空登录成功");
                return true;
            }

            var errMsg = json?["Result"]?["ResponseStatus"]?["Errors"]?.ToJsonString()
                         ?? json?["Message"]?.GetValue<string>()
                         ?? response;
            _logger.LogError("金蝶云星空登录失败：{Msg}", errMsg);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "金蝶云星空登录异常");
            return false;
        }
    }

    /// <summary>
    /// 执行单据查询（物料列表查询）
    /// 接口：Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.executeBillQuery.common.kdsvc
    /// </summary>
    public async Task<List<JsonArray>?> ExecuteBillQueryAsync(string formId, string fieldKeys,
        string? filterString = null, string? orderBy = null, string? topRowCount = null, CancellationToken cancellationToken = default)
    {
        if (!_isLoggedIn)
        {
            var ok = await LoginAsync(cancellationToken);
            if (!ok) return null;
        }

        var serverUrl = NormalizeServerUrl();
        var url = serverUrl + "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.executeBillQuery.common.kdsvc";

        var payload = new JsonObject
        {
            ["FormId"] = formId,
            ["FieldKeys"] = fieldKeys,
            ["FilterString"] = filterString ?? string.Empty,
            ["OrderString"] = orderBy ?? string.Empty,
            ["TopRowCount"] = topRowCount ?? string.Empty,
            ["StartRow"] = 0,
            ["Limit"] = 1000,
            ["SubSystemId"] = string.Empty
        };

        var wrapper = new JsonArray { payload.ToJsonString() };
        var body = BuildRequestPayload(wrapper);

        try
        {
            var response = await SendPostAsync(url, body, cancellationToken);
            var json = JsonNode.Parse(response);

            // executeBillQuery 返回格式：[[field1,field2,...],[...]] 或 {"Result":[[...]]}
            var arr = json as JsonArray;
            if (arr == null)
            {
                arr = json?["Result"]?.AsArray();
                // 某些版本可能是 string 形式的 JSON
                if (arr == null && json is JsonValue jv && jv.TryGetValue<string>(out var s))
                {
                    arr = JsonNode.Parse(s) as JsonArray;
                }
            }

            if (arr == null)
            {
                _logger.LogWarning("executeBillQuery 返回格式异常：{Response}", response[..Math.Min(300, response.Length)]);
                return new List<JsonArray>();
            }

            var rows = new List<JsonArray>();
            foreach (var row in arr)
            {
                if (row is JsonArray ra) rows.Add(ra);
            }
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "executeBillQuery 调用失败");
            return null;
        }
    }

    /// <summary>
    /// 查看单据详情（获取更详细信息和附件/图片信息）
    /// 接口：Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.View.common.kdsvc
    /// </summary>
    public async Task<JsonNode?> ViewAsync(string formId, string idOrNumber, CancellationToken cancellationToken = default)
    {
        if (!_isLoggedIn)
        {
            var ok = await LoginAsync(cancellationToken);
            if (!ok) return null;
        }

        var serverUrl = NormalizeServerUrl();
        var url = serverUrl + "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.View.common.kdsvc";

        // 参数：{ "Id": "...", "Number": "..." } 或 { "FormId": "...", "Id": "..." }
        var payload = new JsonObject
        {
            ["FormId"] = formId
        };

        // 优先尝试按 Number 查询，再尝试按 Id
        Guid.TryParse(idOrNumber, out var _);
        if (idOrNumber.All(char.IsDigit))
        {
            payload["Id"] = idOrNumber;
        }
        else
        {
            payload["Number"] = idOrNumber;
        }

        var wrapper = new JsonArray { payload.ToJsonString() };
        var body = BuildRequestPayload(wrapper);

        try
        {
            var response = await SendPostAsync(url, body, cancellationToken);
            return JsonNode.Parse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "View 调用失败");
            return null;
        }
    }

    /// <summary>
    /// 从 View 接口返回中解析附件/图片信息，拼接成可访问的 URL
    /// </summary>
    public List<string> ExtractImageUrls(JsonNode? viewResult)
    {
        var urls = new List<string>();
        if (viewResult == null) return urls;

        // 尝试多种可能的字段名：物料的图片/附件通常出现在 Result 下的字段或附件字段中
        var candidates = new[]
        {
            "Result/FImage",
            "Result/FImageUrl",
            "Result/FAttchment",
            "Result/FAttachment",
            "Result/FImages",
            "Result/FTopImage",
            "Result/FImagePath",
            "Result/FPicture"
        };

        foreach (var path in candidates)
        {
            var node = TryGetNodeByPath(viewResult, path);
            if (node == null) continue;
            var val = node.GetValue<string?>();
            if (!string.IsNullOrWhiteSpace(val))
            {
                var fullUrl = BuildImageUrl(val);
                if (!string.IsNullOrWhiteSpace(fullUrl) && !urls.Contains(fullUrl)) urls.Add(fullUrl);
            }
        }

        // 遍历所有 string 属性搜索疑似图片路径（简单兜底）
        try
        {
            var allStrings = new List<string>();
            CollectStrings(viewResult, allStrings, depth: 0);
            var imageSuffixes = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            foreach (var s in allStrings.Distinct().Take(50))
            {
                if (string.IsNullOrWhiteSpace(s)) continue;
                if (s.Contains("http"))
                {
                    // 已为完整URL
                    if (imageSuffixes.Any(suf => s.IndexOf(suf, StringComparison.OrdinalIgnoreCase) >= 0) && !urls.Contains(s))
                    {
                        urls.Add(s);
                    }
                }
                else if (imageSuffixes.Any(suf => s.IndexOf(suf, StringComparison.OrdinalIgnoreCase) >= 0) ||
                         s.Contains("images/", StringComparison.OrdinalIgnoreCase) ||
                         s.Contains("fileserver", StringComparison.OrdinalIgnoreCase) ||
                         s.Contains("attach", StringComparison.OrdinalIgnoreCase))
                {
                    var full = BuildImageUrl(s);
                    if (!string.IsNullOrWhiteSpace(full) && !urls.Contains(full)) urls.Add(full);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "解析图片URL出现异常，已跳过");
        }

        return urls;
    }

    private static void CollectStrings(JsonNode? node, List<string> collector, int depth)
    {
        if (node == null || depth > 6) return;
        if (node is JsonValue jv && jv.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
        {
            collector.Add(s);
            return;
        }
        if (node is JsonObject jo)
        {
            foreach (var prop in jo) CollectStrings(prop.Value, collector, depth + 1);
            return;
        }
        if (node is JsonArray ja)
        {
            foreach (var item in ja) CollectStrings(item, collector, depth + 1);
        }
    }

    private static JsonNode? TryGetNodeByPath(JsonNode root, string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var p in parts)
        {
            if (current == null) return null;
            current = current[p];
        }
        return current;
    }

    private string BuildImageUrl(string relativeOrFull)
    {
        if (string.IsNullOrWhiteSpace(relativeOrFull)) return string.Empty;
        if (relativeOrFull.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            relativeOrFull.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return relativeOrFull;
        }

        var server = ImageServer();
        // 常见拼接规则：
        // 1) 纯相对路径：直接拼接
        // 2) fileserver/downloadImage/<path> 形式
        var cleaned = relativeOrFull.TrimStart('/', '\\');
        // 若 path 中已包含 "fileserver" 或 "images" 就直接拼接，否则加常见前缀
        if (cleaned.StartsWith("fileserver", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("images", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("attachment", StringComparison.OrdinalIgnoreCase) ||
            cleaned.StartsWith("tempfile", StringComparison.OrdinalIgnoreCase))
        {
            return server + cleaned;
        }

        // 默认尝试：fileserver/downloadImage/<path>
        return server + "fileserver/downloadImage/" + cleaned;
    }

    /// <summary>
    /// 构建金蝶 WebAPI 标准请求包
    /// </summary>
    private string BuildRequestPayload(JsonArray parameters)
    {
        var obj = new JsonObject
        {
            ["format"] = 1,
            ["useragent"] = "ApiClient",
            ["rid"] = Guid.NewGuid().ToString().GetHashCode().ToString(),
            ["parameters"] = parameters,
            ["timestamp"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["v"] = "1.0"
        };
        return obj.ToJsonString(new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                System.Text.Encodings.Web.TextEncoderSettings.AllowAllRanges)
        });
    }

    private async Task<string> SendPostAsync(string url, string jsonBody, CancellationToken cancellationToken)
    {
        using var content = new StringContent(jsonBody, new UTF8Encoding(false), "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.UserAgent.ParseAdd("KingdeeApiClient/1.0");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        // 金蝶某些版本会返回 response_error:xxx 这种前缀
        if (raw.StartsWith("response_error:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(raw);
        }
        return raw;
    }

    public void ResetLogin()
    {
        lock (_loginLock) { _isLoggedIn = false; _loginAt = DateTime.MinValue; }
    }
}

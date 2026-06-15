using System.Text.Json;
using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;

namespace Kingdee.MaterialAPI;

/// <summary>
/// 把金蝶 API 返回的数据 → Material 模型
/// 字段映射完全来自 AppConfig.FieldMappings
/// </summary>
public class KingdeeMaterialMapper
{
    private readonly AppConfigStore _configStore;

    public KingdeeMaterialMapper(AppConfigStore configStore)
    {
        _configStore = configStore;
    }

    /// <summary>构建 executeBillQuery 要查询的 SELECT 字段（逗号分隔） —— 按字段映射的 KingdeeField 生成</summary>
    public string BuildSelectFields()
    {
        var cfg = _configStore.Get();
        var fields = new List<string>();
        // 物料主键 ID（必须要，用于取详情）
        fields.Add("FMATERIALID");
        // 物料编码（作为物料编号展示）
        fields.Add("FNUMBER");
        foreach (var m in cfg.FieldMappings.Where(x => x.Enabled && x.FieldKey != "images"))
        {
            if (string.IsNullOrWhiteSpace(m.KingdeeField)) continue;
            foreach (var f in m.KingdeeField.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var k = f.Trim();
                if (!fields.Contains(k, StringComparer.OrdinalIgnoreCase))
                    fields.Add(k);
            }
        }
        return string.Join(",", fields);
    }

    /// <summary>把 executeBillQuery 返回的一行 → Material（只填充列表字段）</summary>
    public Material FromRow(object[] row)
    {
        var cfg = _configStore.Get();
        var selectFields = BuildSelectFields().Split(',');
        var mat = new Material();

        // row[0] 通常是 FMATERIALID（主键），row[1] 是 FNUMBER
        if (row.Length > 0) mat.MaterialId = Obj<string>(row[0]);
        if (row.Length > 1) mat.Number = Obj<string>(row[1]);

        // 从 row[2] 起按 selectFields 的顺序与 FieldMappings 对应
        // 但为了避免顺序不一致，这里简单地做"名字→索引"查找
        for (int i = 0; i < selectFields.Length && i < row.Length; i++)
        {
            var fieldName = selectFields[i].Trim();
            var value = row[i];

            // 匹配每一条字段映射
            foreach (var mp in cfg.FieldMappings)
            {
                if (!mp.Enabled) continue;
                if (string.IsNullOrWhiteSpace(mp.KingdeeField)) continue;
                var keys = mp.KingdeeField.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim());
                if (keys.Any(k => string.Equals(k, fieldName, StringComparison.OrdinalIgnoreCase)))
                {
                    SetByFieldKey(mat, mp.FieldKey, value?.ToString() ?? "");
                }
            }
        }
        // 兜底：没有物料名的话用编号
        if (string.IsNullOrWhiteSpace(mat.MaterialName))
            mat.MaterialName = mat.Number;
        return mat;
    }

    /// <summary>从 View 返回的单据详情填充图片 URL（或其他详情字段）</summary>
    public void EnrichFromView(Material mat, JsonDocument viewResult)
    {
        var cfg = _configStore.Get();
        var imageFields = cfg.FieldMappings
            .FirstOrDefault(f => f.FieldKey.Equals("images", StringComparison.OrdinalIgnoreCase));
        if (imageFields == null || string.IsNullOrWhiteSpace(imageFields.KingdeeField)) return;

        var candidates = imageFields.KingdeeField
            .Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToList();

        var urls = new List<string>();
        void Collect(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in el.EnumerateObject())
                {
                    // 候选字段命中
                    if (candidates.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        ExtractUrls(p.Value, urls, cfg);
                    }
                    else
                    {
                        Collect(p.Value);
                    }
                }
            }
            else if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in el.EnumerateArray())
                    Collect(child);
            }
        }

        Collect(viewResult.RootElement);
        mat.Images = urls;
    }

    private static void ExtractUrls(JsonElement val, List<string> urls, AppConfig cfg)
    {
        // 简单提取：字符串且看起来像 URL / 文件名
        if (val.ValueKind == JsonValueKind.String)
        {
            var s = val.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(s)) return;
            if (s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(s);
                return;
            }
            // 可能是金蝶附件相对路径或 fileid
            if (!string.IsNullOrWhiteSpace(cfg.KingdeeImageServerUrl) &&
                (s.Contains("/") || s.Contains("\\") || s.Length > 4))
            {
                urls.Add($"{cfg.KingdeeImageServerUrl.TrimEnd('/')}/fileserver/downloadImage/?fileid={Uri.EscapeDataString(s)}");
            }
        }
        else if (val.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in val.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String)
                {
                    var s = p.Value.GetString() ?? "";
                    if (s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        urls.Add(s);
                    else if (!string.IsNullOrWhiteSpace(s) && !string.IsNullOrWhiteSpace(cfg.KingdeeImageServerUrl))
                        urls.Add($"{cfg.KingdeeImageServerUrl.TrimEnd('/')}/fileserver/downloadImage/?fileid={Uri.EscapeDataString(s)}");
                }
            }
        }
        else if (val.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in val.EnumerateArray())
                ExtractUrls(c, urls, cfg);
        }
    }

    /// <summary>根据 FieldKey 写入 Material 的对应属性（通过反射兜底）</summary>
    private static void SetByFieldKey(Material mat, string fieldKey, string value)
    {
        if (string.IsNullOrWhiteSpace(fieldKey)) return;
        var prop = typeof(Material).GetProperty(
            System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(fieldKey.ToLower())
            );
        if (prop == null)
        {
            // 也尝试直接匹配大小写
            prop = typeof(Material).GetProperty(fieldKey);
        }
        if (prop == null || !prop.CanWrite)
        {
            // 写不到属性，丢进 Extra 字典
            mat.Extra[fieldKey] = value;
            return;
        }
        try
        {
            prop.SetValue(mat, value);
        }
        catch
        {
            // 忽略类型转换错误
        }
    }

    private static T? Obj<T>(object o)
    {
        try { return (T?)Convert.ChangeType(o, typeof(T)); }
        catch { return default; }
    }
}

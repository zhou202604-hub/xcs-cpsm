using System.Text.Json;
using Kingdee.MaterialAPI.Models;

namespace Kingdee.MaterialAPI.Services;

/// <summary>
/// 把金蝶返回的数据 —— 字段名与 Material 的展示字段对应
/// 字段映射来源：AppConfig.FieldMappings
/// </summary>
public class KingdeeMaterialMapper
{
    private readonly AppConfigStore _config;
    private readonly ILogger<KingdeeMaterialMapper> _logger;

    public KingdeeMaterialMapper(AppConfigStore config, ILogger<KingdeeMaterialMapper> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>构建 SELECT 列表（FMATERIALID,FNUMBER + 所有启用字段）</summary>
    public string BuildSelectFields()
    {
        var cfg = _config.Get();
        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FMATERIALID",
            "FNUMBER"
        };
        foreach (var fm in cfg.FieldMappings.Where(x => x.Enabled))
        {
            if (string.IsNullOrWhiteSpace(fm.KingdeeField)) continue;
            // images 字段不参与 executeBillQuery（附件通常需要 View 获取）
            if (fm.FieldKey.Equals("images", StringComparison.OrdinalIgnoreCase)) continue;

            foreach (var k in fm.KingdeeField.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var f = k.Trim();
                if (!string.IsNullOrWhiteSpace(f)) list.Add(f);
            }
        }
        return string.Join(",", list);
    }

    /// <summary>
    /// 从 executeBillQuery 返回的二维数组某一行构建 Material 对象
    /// </summary>
    public Material FromRow(object[] row, string[] selectFields)
    {
        var cfg = _config.Get();
        var mat = new Material();

        // 1) 前两列固定为 FMATERIALID, FNUMBER
        if (row.Length > 0) mat.MaterialId = row[0]?.ToString() ?? "";
        if (row.Length > 1) mat.Number = row[1]?.ToString() ?? "";

        // 2) 其余列按 selectFields 的名字与字段映射匹配
        for (int i = 2; i < Math.Min(selectFields.Length, row.Length); i++)
        {
            var fieldName = selectFields[i].Trim();
            var value = row[i]?.ToString() ?? "";
            MatchAndAssign(mat, fieldName, value, cfg.FieldMappings);
        }

        // 兜底
        if (string.IsNullOrWhiteSpace(mat.MaterialName)) mat.MaterialName = mat.Number;
        return mat;
    }

    /// <summary>从 View 返回的单据详情里抓取图片 URL 列表</summary>
    public List<string> ExtractImageUrls(JsonDocument? viewDoc, string pkValue)
    {
        var cfg = _config.Get();
        var imagesMapping = cfg.FieldMappings.FirstOrDefault(
            f => f.FieldKey.Equals("images", StringComparison.OrdinalIgnoreCase));
        var list = new List<string>();
        if (imagesMapping == null || string.IsNullOrWhiteSpace(imagesMapping.KingdeeField) || viewDoc == null) return list;

        var candidateFields = imagesMapping.KingdeeField
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();

        // 在整个 JSON 中递归寻找字段名命中的属性值
        CollectStringValues(viewDoc.RootElement, candidateFields, list);

        // 对形如 "fileid=xxx" 或简单图片文件名的字段，拼接 ImageServerUrl
        var imgServer = (cfg.Kingdee.ImageServerUrl ?? "").TrimEnd('/');
        for (int i = 0; i < list.Count; i++)
        {
            var v = list[i];
            if (v.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(imgServer))
            {
                list[i] = $"{imgServer}/fileserver/downloadImage/?fileid={Uri.EscapeDataString(v)}";
            }
        }
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void MatchAndAssign(Material mat, string kingdeeField, string value, List<FieldMappingItem> mappings)
    {
        foreach (var fm in mappings.Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.KingdeeField)))
        {
            var fields = fm.KingdeeField.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
            if (fields.Any(f => f.Equals(kingdeeField, StringComparison.OrdinalIgnoreCase)))
            {
                SetMaterialProperty(mat, fm.FieldKey, value);
                return;
            }
        }
    }

    private static void SetMaterialProperty(Material mat, string fieldKey, string value)
    {
        // 大小写不敏感地找属性
        var prop = typeof(Material).GetProperty(
            fieldKey,
            System.Reflection.BindingFlags.IgnoreCase |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            try
            {
                prop.SetValue(mat, value);
                return;
            }
            catch
            {
                // 忽略类型转换失败
            }
        }
        // 找不到对应属性的，扔进 Extra
        mat.Extra[fieldKey] = value;
    }

    private static void CollectStringValues(JsonElement el, List<string> candidateFields, List<string> result)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    // 如果属性名匹配了候选字段名，且是字符串/数组形式，收集
                    if (candidateFields.Contains(p.Name, StringComparer.OrdinalIgnoreCase) ||
                        candidateFields.Any(f => p.Name.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        if (p.Value.ValueKind == JsonValueKind.String)
                        {
                            var s = p.Value.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(s)) result.Add(s);
                        }
                        else if (p.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in p.Value.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    var s = item.GetString() ?? "";
                                    if (!string.IsNullOrWhiteSpace(s)) result.Add(s);
                                }
                            }
                        }
                    }
                    // 继续递归，以便附件可能藏在更深层级
                    CollectStringValues(p.Value, candidateFields, result);
                }
                break;
            case JsonValueKind.Array:
                foreach (var c in el.EnumerateArray())
                    CollectStringValues(c, candidateFields, result);
                break;
        }
    }
}

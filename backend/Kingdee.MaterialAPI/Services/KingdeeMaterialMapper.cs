using System.Text.Json.Nodes;
using Kingdee.MaterialAPI.Models;

namespace Kingdee.MaterialAPI.Services;

/// <summary>
/// 金蝶物料字段映射与转换器
/// 用于把金蝶云星空返回的数据转换成统一的 Material 模型
/// </summary>
public static class KingdeeMaterialMapper
{
    /// <summary>
    /// executeBillQuery 推荐查询字段（可按需扩展）
    /// </summary>
    public const string DefaultFieldKeys =
        "FMATERIALID," +          // 0 内码
        "FNUMBER," +               // 1 物料编码
        "FNAME," +                 // 2 物料名称
        "FSPECIFICATION," +        // 3 规格型号
        "FBASEUNITID__FNAME," +    // 4 基本单位名称（物料多计量单位基础资料）
        "FMATERIALGROUP__FNUMBER," // 5 物料分组编码
        ;

    /// <summary>
    /// 将 executeBillQuery 单行数据转换为 Material
    /// 字段可根据客户实际账套的自定义字段调整
    /// </summary>
    public static Material? FromRow(JsonArray row)
    {
        if (row == null || row.Count == 0) return null;

        var cols = row.Select(c => c?.GetValue<string>()?.Trim() ?? string.Empty).ToList();
        while (cols.Count < 6) cols.Add(string.Empty);

        var m = new Material
        {
            Id = cols[0] ?? cols[1] ?? Guid.NewGuid().ToString("N"),
            MaterialName = cols[2],
            CommonName = cols[2], // 通用名若客户有对应字段可在此替换
            SpecModel = cols[3],
            BaseUnit = cols[4],
            // 物料等级/剂型/产品属性等通常需要从基础资料/自定义字段中读取
            // 这里默认用物料分组的编码来推断，方便测试
            MaterialLevel = GuessLevel(cols[5], cols[2])
        };

        return m;
    }

    private static string GuessLevel(string groupCode, string name)
    {
        if (string.IsNullOrWhiteSpace(groupCode) && string.IsNullOrWhiteSpace(name)) return "二级";

        var text = (groupCode + " " + name).ToLowerInvariant();
        if (text.Contains("一级") || text.Contains("level1") || text.Contains("原药")) return "一级";
        if (text.Contains("三级") || text.Contains("level3") || text.Contains("助剂")) return "三级";
        return "二级";
    }

    /// <summary>
    /// 从 View 返回结构中填充更详细的物料信息
    /// </summary>
    public static void EnrichFromView(Material material, JsonNode? viewResult)
    {
        if (material == null || viewResult == null) return;

        // Result.Model 下通常包含完整单据，尝试读取常见字段
        var model = viewResult["Result"]?["Model"];
        if (model == null)
        {
            model = viewResult["Result"];
        }
        if (model == null) return;

        // 基础字段（字段名可能因版本略有差异）
        material.MaterialName = PickString(model, "FName", "FMATERIALID_FNAME") ?? material.MaterialName;
        material.SpecModel = PickString(model, "FSPECIFICATION", "FSpec") ?? material.SpecModel;
        material.BaseUnit = PickString(model, "FBaseUnitId__FName", "FUnitName") ?? material.BaseUnit;

        // 通用名：通常在客户自定义字段，如 FCommonName
        material.CommonName = PickString(model, "FCommonName", "FCommonName_kd", "FMaterialName") ?? material.CommonName;

        // 登记剂型、产品属性等
        material.DosageForm = PickString(model, "FDosageForm", "FForm") ?? string.Empty;
        material.ProductAttribute = PickString(model, "FProductAttr", "FProductAttribute") ?? string.Empty;
        material.CropAttribute = PickString(model, "FCropAttr", "FCropAttribute") ?? string.Empty;
        material.CropSite = PickString(model, "FCropSite", "FUseScene") ?? string.Empty;
        material.ControlTarget = PickString(model, "FTarget", "FControlTarget") ?? string.Empty;
        material.UsageTime = PickString(model, "FUseTime", "FUsagePeriod") ?? string.Empty;
        material.RegistrationNo = PickString(model, "FRegNo", "FRegNumber") ?? string.Empty;
        material.ProductManager = PickString(model, "FProductMgr", "FProductManager") ?? string.Empty;
        material.ProductInfo = PickString(model, "FDescription", "FRemark", "FInfo") ?? string.Empty;

        // 物料等级可根据基础资料中的字段
        if (string.IsNullOrWhiteSpace(material.MaterialLevel))
        {
            material.MaterialLevel = PickString(model, "FMaterialLevel", "FLevel") ?? "二级";
        }
    }

    private static string? PickString(JsonNode node, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (node[key] is JsonValue jv && jv.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
            // 支持基础资料：FKey__FName
            var parts = key.Split("__", StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && node[parts[0]] is JsonObject subNode)
            {
                var val = subNode[parts[1]]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }
        return null;
    }
}

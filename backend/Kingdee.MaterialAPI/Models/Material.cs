namespace Kingdee.MaterialAPI.Models;

/// <summary>
/// 物料信息（前端要展示的字段集合）
/// 字段对应规则：见管理后台的"字段映射"——每一行的 FieldKey 匹配到本类的属性（大小写不敏感）
/// </summary>
public class Material
{
    /// <summary>金蝶物料主键（FMATERIALID）</summary>
    public string MaterialId { get; set; } = "";

    /// <summary>物料编号（FNUMBER）</summary>
    public string Number { get; set; } = "";

    public string MaterialName { get; set; } = "";
    public string? GeneralName { get; set; }
    public string? BasicUnit { get; set; }
    public string? Specification { get; set; }
    public string? MaterialLevel { get; set; }
    public string? RegistrationForm { get; set; }
    public string? CropPlace { get; set; }
    public string? ControlTarget { get; set; }
    public string? UseTime { get; set; }
    public string? RegistrationNo { get; set; }
    public string? ProductAttribute { get; set; }
    public string? CropAttribute { get; set; }
    public string? ProductManager { get; set; }
    public string? ProductInfo { get; set; }

    /// <summary>物料图片 URL 列表（支持多图片）</summary>
    public List<string>? Images { get; set; }

    /// <summary>字段映射未匹配到属性时会落进此字典（方便前端 "查看全部"）</summary>
    public Dictionary<string, string> Extra { get; set; } = new Dictionary<string, string>();
}

/// <summary>物料查询参数</summary>
public class MaterialQueryParams
{
    public string? Keyword { get; set; }
    public string? Level { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>统一 API 响应格式</summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public T? Data { get; set; }
}

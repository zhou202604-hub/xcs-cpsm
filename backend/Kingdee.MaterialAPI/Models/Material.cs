namespace Kingdee.MaterialAPI.Models;

/// <summary>
/// 物料对象：移动端前端列表/详情要展示的数据
/// 字段名 + 顺序 与 前端期望一致
/// </summary>
public class Material
{
    public string MaterialId { get; set; } = "";    // 金蝶物料主键
    public string Number { get; set; } = "";         // 物料编码
    public string MaterialName { get; set; } = "";   // 物料名称
    public string? GeneralName { get; set; }         // 通用名
    public string? BasicUnit { get; set; }           // 基本单位
    public string? Specification { get; set; }       // 规格型号
    public string? MaterialLevel { get; set; }       // 物料等级
    public string? RegistrationForm { get; set; }    // 登记剂型
    public string? CropPlace { get; set; }           // 作物场所
    public string? ControlTarget { get; set; }       // 防治对象
    public string? UseTime { get; set; }             // 大概使用时间
    public string? RegistrationNo { get; set; }      // 登记证号
    public string? ProductAttribute { get; set; }    // 产品属性
    public string? CropAttribute { get; set; }       // 作物属性
    public string? ProductManager { get; set; }      // 产品经理
    public string? ProductInfo { get; set; }         // 产品信息

    /// <summary>物料图片 URL 列表</summary>
    public List<string>? Images { get; set; }

    /// <summary>字段映射中没有定义的字段（调试用）</summary>
    public Dictionary<string, string> Extra { get; set; } = new();
}

/// <summary>物料查询请求参数</summary>
public class MaterialQueryParams
{
    public string? Keyword { get; set; }
    public string? Level { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>统一 API 响应包</summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
public class ApiResponse<T> : ApiResponse
{
    public T? Data { get; set; }
}

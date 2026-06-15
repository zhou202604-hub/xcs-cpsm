namespace Kingdee.MaterialAPI.Models;

/// <summary>
/// 物料信息模型
/// </summary>
public class Material
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 物料名称
    /// </summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>
    /// 通用名
    /// </summary>
    public string CommonName { get; set; } = string.Empty;

    /// <summary>
    /// 基本单位
    /// </summary>
    public string BaseUnit { get; set; } = string.Empty;

    /// <summary>
    /// 规格型号
    /// </summary>
    public string SpecModel { get; set; } = string.Empty;

    /// <summary>
    /// 物料等级
    /// </summary>
    public string MaterialLevel { get; set; } = string.Empty;

    /// <summary>
    /// 登记剂型
    /// </summary>
    public string DosageForm { get; set; } = string.Empty;

    /// <summary>
    /// 作物场所
    /// </summary>
    public string CropSite { get; set; } = string.Empty;

    /// <summary>
    /// 防治对象
    /// </summary>
    public string ControlTarget { get; set; } = string.Empty;

    /// <summary>
    /// 大概使用时间
    /// </summary>
    public string UsageTime { get; set; } = string.Empty;

    /// <summary>
    /// 登记证号
    /// </summary>
    public string RegistrationNo { get; set; } = string.Empty;

    /// <summary>
    /// 产品属性
    /// </summary>
    public string ProductAttribute { get; set; } = string.Empty;

    /// <summary>
    /// 作物属性
    /// </summary>
    public string CropAttribute { get; set; } = string.Empty;

    /// <summary>
    /// 产品经理
    /// </summary>
    public string ProductManager { get; set; } = string.Empty;

    /// <summary>
    /// 产品信息
    /// </summary>
    public string ProductInfo { get; set; } = string.Empty;
}

/// <summary>
/// 物料查询参数
/// </summary>
public class MaterialQueryParams
{
    public string? Keyword { get; set; }
    public string? Level { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// API响应结果
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public int TotalCount { get; set; }
}

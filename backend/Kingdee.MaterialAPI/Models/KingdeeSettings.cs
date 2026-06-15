namespace Kingdee.MaterialAPI.Models;

/// <summary>
/// 金蝶云星空API配置
/// </summary>
public class KingdeeSettings
{
    /// <summary>
    /// 是否启用金蝶API（为 false 时使用内置 Mock 数据）
    /// </summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// 金蝶云星空站点地址，如：http://192.168.3.5/k3cloud/
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// 账套/数据中心ID
    /// </summary>
    public string DbId { get; set; } = string.Empty;

    /// <summary>
    /// 登录用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 登录密码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 语言标识：简体中文=2052，繁体中文=3076，英文=1033
    /// </summary>
    public int LcId { get; set; } = 2052;

    /// <summary>
    /// 请求超时（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 图片服务器前缀（用于拼接附件URL），如与 ServerUrl 相同可留空
    /// </summary>
    public string? ImageServerUrl { get; set; }
}

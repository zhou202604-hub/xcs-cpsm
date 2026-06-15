namespace Kingdee.MaterialAPI.Models;

/// <summary>
/// 企业微信自建应用配置
/// </summary>
public class WeComSettings
{
    /// <summary>
    /// 企业ID（CorpId）
    /// </summary>
    public string CorpId { get; set; } = string.Empty;

    /// <summary>
    /// 应用 AgentId
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// 应用 Secret
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// 回调地址（完整 URL），例如 https://material.your-company.com/api/auth
    /// </summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>
    /// JWT 签名密钥（至少 16 字符）
    /// </summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>
    /// JWT 有效期（小时）
    /// </summary>
    public int JwtExpireHours { get; set; } = 24;

    /// <summary>
    /// 是否强制企业微信登录（true 时未登录会自动跳 OAuth 页面）
    /// </summary>
    public bool ForceWeComLogin { get; set; } = true;
}

namespace Kingdee.MaterialAPI.Models;

/// <summary>金蝶服务器信息（来自管理后台保存的配置）</summary>
public class KingdeeSettings
{
    public bool Enable { get; set; }
    public string ServerUrl { get; set; } = "";       // e.g. http://kd-server/
    public string DbId { get; set; } = "";            // 账套 ID
    public string UserName { get; set; } = "";        // 用户名
    public string Password { get; set; } = "";        // 密码
    public int LcId { get; set; } = 2052;             // 语言 ID（中文 2052）
    public int TimeoutSeconds { get; set; } = 30;
    public string ImageServerUrl { get; set; } = "";  // 可选：图片附件服务器
    public string MaterialFormId { get; set; } = "BD_MATERIAL"; // 物料表单 ID
}

/// <summary>字段映射条目</summary>
public class FieldMappingItem
{
    /// <summary>前端字段 Key（匹配 Material 的属性名，大小写不敏感）</summary>
    public string FieldKey { get; set; } = "";
    /// <summary>展示名（管理后台可见）</summary>
    public string Label { get; set; } = "";
    /// <summary>金蝶侧字段名，多个用英文逗号分隔</summary>
    public string KingdeeField { get; set; } = "";
    /// <summary>是否启用该字段</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>备注说明</summary>
    public string? Remark { get; set; }
}

/// <summary>企业微信配置</summary>
public class WeComSettings
{
    public bool Enable { get; set; }
    public string CorpId { get; set; } = "";          // 企业 ID
    public string AgentId { get; set; } = "";         // 应用 AgentId
    public string Secret { get; set; } = "";          // 应用 Secret
    public string CallbackUrl { get; set; } = "";     // https://域名/api/auth
    public string JwtSecret { get; set; } = "";       // 自制 Token 签名密钥（>=16 字符）
    public int JwtExpireHours { get; set; } = 24;
    public bool ForceWeComLogin { get; set; } = true; // 是否强制走企业微信
}

/// <summary>应用根配置（映射到 app_data/config.json）</summary>
public class AppConfig
{
    public KingdeeSettings Kingdee { get; set; } = new();
    public List<FieldMappingItem> FieldMappings { get; set; } = new();
    public WeComSettings WeCom { get; set; } = new();
    public string AdminPassword { get; set; } = "admin123";
}

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kingdee.MaterialAPI.Services;

/// <summary>
/// 应用全局配置（金蝶连接、字段映射、企业微信等）
/// 持久化到 app_data/config.json
/// 热更新：保存后下次请求立即生效
/// </summary>
public class AppConfig
{
    // -------- 金蝶连接 --------
    public bool KingdeeEnable { get; set; }
    public string KingdeeServerUrl { get; set; } = "";
    public string KingdeeDbId { get; set; } = "";
    public string KingdeeUserName { get; set; } = "";
    public string KingdeePassword { get; set; } = "";
    public int KingdeeLcId { get; set; } = 2052;
    public int KingdeeTimeoutSeconds { get; set; } = 30;
    public string KingdeeImageServerUrl { get; set; } = "";
    /// <summary>金蝶物料主表单 ID，一般是 BD_MATERIAL</summary>
    public string KingdeeMaterialFormId { get; set; } = "BD_MATERIAL";

    // -------- 字段映射：前端显示字段 -> 金蝶字段标识 --------
    public List<FieldMappingItem> FieldMappings { get; set; } = new();

    // -------- 企业微信 --------
    public bool WeComEnable { get; set; }
    public string WeComCorpId { get; set; } = "";
    public string WeComAgentId { get; set; } = "";
    public string WeComSecret { get; set; } = "";
    public string WeComCallbackUrl { get; set; } = "";
    public string WeComJwtSecret { get; set; } = "";
    public int WeComJwtExpireHours { get; set; } = 24;
    public bool WeComForceLogin { get; set; } = true;

    // -------- 管理后台登录密码 --------
    /// <summary>管理员密码（明文保存，因为内网使用场景；生产请部署在 HTTPS 内网并改复杂密码）</summary>
    public string AdminPassword { get; set; } = "admin123";
}

/// <summary>
/// 字段映射条目
/// </summary>
public class FieldMappingItem
{
    /// <summary>前端字段 Key（不可变，小写约定，如 materialName / registrationNo）</summary>
    public string FieldKey { get; set; } = "";
    /// <summary>显示名称（中文标签，eg "物料名称"）</summary>
    public string Label { get; set; } = "";
    /// <summary>金蝶字段标识（eg FName / FNumber / FSpecification）</summary>
    public string KingdeeField { get; set; } = "";
    /// <summary>是否在移动端列表/详情页启用（未启用的不显示也不查询）</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>是否在列表卡片显示（true=在列表中显示该行；false=仅详情页显示）</summary>
    public bool ShowInList { get; set; }
    /// <summary>排序号（小的在前）</summary>
    public int Order { get; set; }
    /// <summary>备注说明（仅管理后台可见）</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 配置读写服务
/// - 文件路径：{应用目录}/app_data/config.json
/// - 支持热更新（保存后下一次请求读最新值）
/// - 第一次启动会自动写入默认值
/// </summary>
public class AppConfigStore
{
    private readonly string _filePath;
    private readonly ILogger<AppConfigStore> _logger;
    private static readonly object _lock = new();

    // 缓存 + 简单过期
    private AppConfig _cached;
    private DateTime _cacheExpire = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AppConfigStore(ILogger<AppConfigStore> logger)
    {
        _logger = logger;
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "app_data");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "config.json");
        _cached = LoadInternal();
    }

    /// <summary>读取当前配置（优先走缓存）</summary>
    public AppConfig Get()
    {
        if (_cacheExpire < DateTime.Now)
        {
            _cached = LoadInternal();
            _cacheExpire = DateTime.Now.AddSeconds(5);
        }
        return _cached;
    }

    /// <summary>保存配置（立即生效，缓存过期时间重置）</summary>
    public void Save(AppConfig cfg)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(cfg, JsonOpts);
            File.WriteAllText(_filePath, json);
            _cached = cfg;
            _cacheExpire = DateTime.Now.AddSeconds(5);
            _logger.LogInformation("配置已保存到 {Path}", _filePath);
        }
    }

    public string FilePath => _filePath;

    // ---------------- 内部 ----------------
    private AppConfig LoadInternal()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    var def = DefaultConfig();
                    File.WriteAllText(_filePath, JsonSerializer.Serialize(def, JsonOpts));
                    _logger.LogInformation("未找到配置文件，已写入默认配置：{Path}", _filePath);
                    return def;
                }
                var json = File.ReadAllText(_filePath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                if (cfg == null)
                {
                    _logger.LogWarning("配置文件解析失败，使用默认值");
                    return DefaultConfig();
                }
                // 兜底：字段映射为空时写入默认
                if (cfg.FieldMappings == null || cfg.FieldMappings.Count == 0)
                {
                    cfg.FieldMappings = DefaultFieldMappings();
                    Save(cfg);
                }
                return cfg;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取配置文件失败，使用默认值");
                return DefaultConfig();
            }
        }
    }

    private static AppConfig DefaultConfig() => new()
    {
        KingdeeEnable = false,
        KingdeeServerUrl = "http://your-kingdee-server/k3cloud/",
        KingdeeDbId = "your-database-id",
        KingdeeUserName = "your-username",
        KingdeePassword = "your-password",
        KingdeeLcId = 2052,
        KingdeeTimeoutSeconds = 30,
        KingdeeMaterialFormId = "BD_MATERIAL",
        FieldMappings = DefaultFieldMappings(),
        WeComEnable = false,
        WeComCorpId = "ww0000000000000000",
        WeComAgentId = "1000001",
        WeComSecret = "your-agent-secret",
        WeComCallbackUrl = "https://material.your-company.com/api/auth",
        WeComJwtSecret = "please-change-this-to-a-long-random-secret-key",
        WeComJwtExpireHours = 24,
        WeComForceLogin = true,
        AdminPassword = "admin123"
    };

    /// <summary>默认字段映射 —— 注意金蝶字段标识需要在管理后台按真实环境修改</summary>
    private static List<FieldMappingItem> DefaultFieldMappings() => new()
    {
        new() { FieldKey = "materialName",     Label = "物料名称",   KingdeeField = "FName",          Enabled = true, ShowInList = true,  Order = 1 },
        new() { FieldKey = "generalName",      Label = "通用名",     KingdeeField = "FDescription",   Enabled = true, ShowInList = true,  Order = 2 },
        new() { FieldKey = "basicUnit",        Label = "基本单位",   KingdeeField = "FBaseUnitId",    Enabled = true, ShowInList = true,  Order = 3 },
        new() { FieldKey = "specification",    Label = "规格型号",   KingdeeField = "FSpecification", Enabled = true, ShowInList = true,  Order = 4 },
        new() { FieldKey = "materialLevel",    Label = "物料等级",   KingdeeField = "FLevel",         Enabled = true, ShowInList = true,  Order = 5 },
        new() { FieldKey = "registrationForm", Label = "登记剂型",   KingdeeField = "FDosageForm",    Enabled = true, ShowInList = false, Order = 6 },
        new() { FieldKey = "cropPlace",        Label = "作物场所",   KingdeeField = "FCropPlace",     Enabled = true, ShowInList = false, Order = 7 },
        new() { FieldKey = "controlTarget",    Label = "防治对象",   KingdeeField = "FTarget",        Enabled = true, ShowInList = false, Order = 8 },
        new() { FieldKey = "useTime",          Label = "大概使用时间",KingdeeField = "FUsePeriod",     Enabled = true, ShowInList = false, Order = 9 },
        new() { FieldKey = "registrationNo",   Label = "登记证号",   KingdeeField = "FRegNo",         Enabled = true, ShowInList = true,  Order = 10 },
        new() { FieldKey = "productAttribute", Label = "产品属性",   KingdeeField = "FProductAttr",   Enabled = true, ShowInList = false, Order = 11 },
        new() { FieldKey = "cropAttribute",    Label = "作物属性",   KingdeeField = "FCropAttr",      Enabled = true, ShowInList = false, Order = 12 },
        new() { FieldKey = "productManager",   Label = "产品经理",   KingdeeField = "FProductManager",Enabled = true, ShowInList = true,  Order = 13 },
        new() { FieldKey = "productInfo",      Label = "产品信息",   KingdeeField = "FProductInfo",   Enabled = true, ShowInList = false, Order = 14 },
        // 图片字段：金蝶物料通常会有附件/图片的字段；常见是 FImage / F_PAOP 等；真实值需在金蝶后台确认
        new() { FieldKey = "images",           Label = "图片字段",   KingdeeField = "FImage",         Enabled = true, ShowInList = false, Order = 99,
                Remark = "多个字段用英文逗号分隔，例如 FImage,FAttachment,F_PICTURE；会自动尝试解析附件中的图片 URL" }
    };
}

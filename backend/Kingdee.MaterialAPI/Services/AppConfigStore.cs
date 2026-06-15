using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kingdee.MaterialAPI.Models;

namespace Kingdee.MaterialAPI.Services;

/// <summary>
/// 应用配置持久化服务
/// - 将配置保存在：应用目录/app_data/config.json
/// - 提供热更新：保存后下次请求立即生效
/// - 首次启动自动写入默认值
/// </summary>
public class AppConfigStore
{
    private readonly string _filePath;
    private readonly ILogger<AppConfigStore> _logger;

    // 简易内存缓存
    private AppConfig? _cached;
    private DateTime _cacheExpire = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null // 保持 C# 属性名（PascalCase），方便前后端一致
    };

    public AppConfigStore(ILogger<AppConfigStore> logger)
    {
        _logger = logger;
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "app_data");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            _logger.LogInformation("创建配置目录: {Dir}", dir);
        }
        _filePath = Path.Combine(dir, "config.json");
        _logger.LogInformation("配置文件: {Path}", _filePath);

        // 首次启动写默认值
        if (!File.Exists(_filePath))
        {
            _cached = DefaultConfig();
            SaveInternal(_cached);
            _logger.LogInformation("已写入默认配置: {Path}", _filePath);
        }
        else
        {
            _cached = LoadInternal();
        }
    }

    /// <summary>读取当前配置（使用缓存，10 秒内重复调用不会反复读盘）</summary>
    public AppConfig Get()
    {
        if (_cacheExpire < DateTime.Now)
        {
            _cached = LoadInternal();
            _cacheExpire = DateTime.Now.AddSeconds(10);
        }
        return _cached!;
    }

    /// <summary>保存配置</summary>
    public void Save(AppConfig cfg)
    {
        SaveInternal(cfg ?? throw new ArgumentNullException(nameof(cfg)));
        _cached = cfg;
        _cacheExpire = DateTime.Now.AddSeconds(10);
        _logger.LogInformation("配置已保存");
    }

    /// <summary>配置文件路径</summary>
    public string FilePath => _filePath;

    // ---------- 内部 ----------
    private AppConfig LoadInternal()
    {
        try
        {
            var json = File.ReadAllText(_filePath);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
            if (cfg == null) return DefaultConfig();
            if (cfg.FieldMappings == null || cfg.FieldMappings.Count == 0)
            {
                cfg.FieldMappings = DefaultFieldMappings();
                SaveInternal(cfg);
            }
            if (cfg.Kingdee == null) cfg.Kingdee = new KingdeeSettings();
            if (cfg.WeCom == null) cfg.WeCom = new WeComSettings();
            if (string.IsNullOrWhiteSpace(cfg.AdminPassword)) cfg.AdminPassword = "admin123";
            return cfg;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "读取配置文件失败，使用默认值");
            return DefaultConfig();
        }
    }

    private void SaveInternal(AppConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, JsonOpts);
        File.WriteAllText(_filePath, json);
    }

    // ---------- 默认值 ----------
    private static AppConfig DefaultConfig()
    {
        return new AppConfig
        {
            Kingdee = new KingdeeSettings
            {
                Enable = false,
                ServerUrl = "",
                DbId = "",
                UserName = "",
                Password = "",
                LcId = 2052,
                TimeoutSeconds = 30,
                ImageServerUrl = "",
                MaterialFormId = "BD_MATERIAL"
            },
            FieldMappings = DefaultFieldMappings(),
            WeCom = new WeComSettings
            {
                Enable = false,
                CorpId = "",
                AgentId = "",
                Secret = "",
                CallbackUrl = "",
                JwtSecret = Guid.NewGuid().ToString("n") + Guid.NewGuid().ToString("n"),
                JwtExpireHours = 24,
                ForceWeComLogin = true
            },
            AdminPassword = "admin123"
        };
    }

    private static List<FieldMappingItem> DefaultFieldMappings()
    {
        // 以下字段名为金蝶标准 BD_MATERIAL 表单中比较常见的英文标识，
        // 实际使用时请在管理后台按贵司实际字段名修正。
        return new List<FieldMappingItem>
        {
            new() { FieldKey = "materialName",     Label = "物料名称",   KingdeeField = "FName",           Enabled = true, Remark = "物料名称" },
            new() { FieldKey = "generalName",      Label = "通用名",     KingdeeField = "FDescription",    Enabled = true, Remark = "通用名 / 描述" },
            new() { FieldKey = "basicUnit",        Label = "基本单位",   KingdeeField = "FBaseUnitId",     Enabled = true, Remark = "基本单位（需用单位名映射时请在金蝶侧转换）" },
            new() { FieldKey = "specification",    Label = "规格型号",   KingdeeField = "FSpecification",  Enabled = true },
            new() { FieldKey = "materialLevel",    Label = "物料等级",   KingdeeField = "FLevel",          Enabled = true, Remark = "可能需要在金蝶侧创建自定义字段" },
            new() { FieldKey = "registrationForm", Label = "登记剂型",   KingdeeField = "FDosageForm",     Enabled = true, Remark = "自定义字段，按实际金蝶字段修正" },
            new() { FieldKey = "cropPlace",        Label = "作物场所",   KingdeeField = "FCropPlace",      Enabled = true },
            new() { FieldKey = "controlTarget",    Label = "防治对象",   KingdeeField = "FTargetPest",     Enabled = true },
            new() { FieldKey = "useTime",          Label = "大概使用时间",KingdeeField = "FUsePeriod",      Enabled = true },
            new() { FieldKey = "registrationNo",   Label = "登记证号",   KingdeeField = "FRegNo",          Enabled = true },
            new() { FieldKey = "productAttribute", Label = "产品属性",   KingdeeField = "FProductAttr",    Enabled = true },
            new() { FieldKey = "cropAttribute",    Label = "作物属性",   KingdeeField = "FCropAttr",       Enabled = true },
            new() { FieldKey = "productManager",   Label = "产品经理",   KingdeeField = "FProductManager", Enabled = true },
            new() { FieldKey = "productInfo",      Label = "产品信息",   KingdeeField = "FProductInfo",    Enabled = true },
            new() { FieldKey = "images",           Label = "图片字段",   KingdeeField = "FImageId,FImgUrl",Enabled = true, Remark = "多个字段用英文逗号分隔；可为附件 ID 或 URL" }
        };
    }
}

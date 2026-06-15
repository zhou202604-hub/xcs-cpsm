using Kingdee.MaterialAPI.Models;
using Microsoft.Extensions.Options;

namespace Kingdee.MaterialAPI.Services;

/// <summary>
/// 物料服务接口
/// </summary>
public interface IMaterialService
{
    Task<ApiResponse<List<Material>>> GetMaterialsAsync(MaterialQueryParams query);
    Task<ApiResponse<Material>> GetMaterialByIdAsync(string id);
}

/// <summary>
/// 物料服务实现
/// - 当配置 Enable = true 时调用金蝶云星空 Web API
/// - 否则返回内置 Mock 数据（便于演示/联调）
/// </summary>
public class MaterialService : IMaterialService
{
    private readonly KingdeeSettings _settings;
    private readonly KingdeeApiClient? _kingdee;
    private readonly ILogger<MaterialService> _logger;

    // 一些公开的示例图 URL，用于演示图片轮播
    private static readonly string[] DemoImages = new[]
    {
        "https://images.unsplash.com/photo-1560493676-04071c5f467b?w=800&q=80",
        "https://images.unsplash.com/photo-1625246333195-78d9c38ad449?w=800&q=80",
        "https://images.unsplash.com/photo-1592982537447-74608774e56d?w=800&q=80",
        "https://images.unsplash.com/photo-1574949966261-9562ae68a82c?w=800&q=80"
    };

    private static readonly List<Material> MockMaterials = new()
    {
        new Material { Id = "WL-00001", MaterialName = "20%草铵膦水剂", CommonName = "草铵膦", BaseUnit = "升", SpecModel = "5L/桶", MaterialLevel = "一级", DosageForm = "水剂", CropSite = "果园、非耕地", ControlTarget = "牛筋草、狗尾草、马唐等一年生杂草", UsageTime = "杂草3-5叶期", RegistrationNo = "PD20200012", ProductAttribute = "除草剂", CropAttribute = "大田作物", ProductManager = "张伟", ProductInfo = "本产品为触杀型除草剂，对多种一年生和多年生杂草有良好防效。施药后6小时遇雨不影响药效。", Images = new List<string>{ DemoImages[0], DemoImages[1], DemoImages[2] } },
        new Material { Id = "WL-00002", MaterialName = "40%多菌灵悬浮剂", CommonName = "多菌灵", BaseUnit = "千克", SpecModel = "1kg/袋", MaterialLevel = "一级", DosageForm = "悬浮剂", CropSite = "蔬菜、果树、水稻", ControlTarget = "稻瘟病、纹枯病、白粉病、炭疽病", UsageTime = "发病初期", RegistrationNo = "PD20180345", ProductAttribute = "杀菌剂", CropAttribute = "经济作物", ProductManager = "李娜", ProductInfo = "广谱性杀菌剂，具有保护和治疗作用。可用于防治多种作物的真菌性病害。", Images = new List<string>{ DemoImages[0], DemoImages[3] } },
        new Material { Id = "WL-00003", MaterialName = "25%吡虫啉可湿性粉剂", CommonName = "吡虫啉", BaseUnit = "克", SpecModel = "100g/袋", MaterialLevel = "二级", DosageForm = "可湿性粉剂", CropSite = "小麦、水稻、蔬菜、果树", ControlTarget = "蚜虫、飞虱、蓟马、粉虱等刺吸式口器害虫", UsageTime = "害虫发生初期", RegistrationNo = "PD20190789", ProductAttribute = "杀虫剂", CropAttribute = "大田作物", ProductManager = "王强", ProductInfo = "烟碱类杀虫剂，具有内吸、触杀和胃毒作用。对刺吸式口器害虫有特效，持效期长。", Images = new List<string>{ DemoImages[1] } },
        new Material { Id = "WL-00004", MaterialName = "5%阿维菌素乳油", CommonName = "阿维菌素", BaseUnit = "毫升", SpecModel = "500ml/瓶", MaterialLevel = "二级", DosageForm = "乳油", CropSite = "蔬菜、果树、棉花", ControlTarget = "红蜘蛛、斑潜蝇、菜青虫、棉铃虫", UsageTime = "低龄幼虫期", RegistrationNo = "PD20170567", ProductAttribute = "杀虫剂", CropAttribute = "经济作物", ProductManager = "陈芳", ProductInfo = "生物源杀虫剂，具有触杀和胃毒作用。对螨类和鳞翅目幼虫有良好防效。", Images = new List<string>{ DemoImages[0], DemoImages[1], DemoImages[2], DemoImages[3] } },
        new Material { Id = "WL-00005", MaterialName = "95%草甘膦原药", CommonName = "草甘膦", BaseUnit = "千克", SpecModel = "25kg/袋", MaterialLevel = "三级", DosageForm = "原药", CropSite = "非耕地、果园行间", ControlTarget = "一年生及多年生杂草", UsageTime = "杂草生长旺盛期", RegistrationNo = "PD20150234", ProductAttribute = "除草剂", CropAttribute = "非耕地", ProductManager = "刘洋", ProductInfo = "广谱灭生性除草剂，通过植物茎叶吸收后传导至根部。用于非耕地除草效果显著。", Images = new List<string>() },
        new Material { Id = "WL-00006", MaterialName = "15%氟磺胺草醚乳油", CommonName = "氟磺胺草醚", BaseUnit = "升", SpecModel = "1L/瓶", MaterialLevel = "二级", DosageForm = "乳油", CropSite = "大豆田、花生田", ControlTarget = "反枝苋、马齿苋、藜等阔叶杂草", UsageTime = "大豆2-4片复叶期", RegistrationNo = "PD20210456", ProductAttribute = "除草剂", CropAttribute = "豆科作物", ProductManager = "赵敏", ProductInfo = "二苯醚类选择性苗后除草剂，用于大豆和花生田防除阔叶杂草。", Images = new List<string>{ DemoImages[2] } },
        new Material { Id = "WL-00007", MaterialName = "80%代森锰锌可湿性粉剂", CommonName = "代森锰锌", BaseUnit = "千克", SpecModel = "2kg/袋", MaterialLevel = "一级", DosageForm = "可湿性粉剂", CropSite = "果树、蔬菜、大田作物", ControlTarget = "霜霉病、疫病、炭疽病、叶斑病", UsageTime = "发病前或发病初期", RegistrationNo = "PD20160890", ProductAttribute = "杀菌剂", CropAttribute = "经济作物", ProductManager = "张伟", ProductInfo = "广谱保护性杀菌剂，含锰、锌微量元素。能有效防治多种真菌性病害。", Images = new List<string>{ DemoImages[0] } },
        new Material { Id = "WL-00008", MaterialName = "20%噻虫嗪水分散粒剂", CommonName = "噻虫嗪", BaseUnit = "克", SpecModel = "200g/袋", MaterialLevel = "二级", DosageForm = "水分散粒剂", CropSite = "水稻、小麦、棉花、蔬菜", ControlTarget = "稻飞虱、蚜虫、蓟马、白粉虱", UsageTime = "害虫发生初期", RegistrationNo = "PD20200678", ProductAttribute = "杀虫剂", CropAttribute = "大田作物", ProductManager = "李娜", ProductInfo = "新一代烟碱类杀虫剂，具有胃毒、触杀和内吸活性。杀虫谱广、活性高、持效期长。", Images = new List<string>{ DemoImages[1], DemoImages[2] } },
        new Material { Id = "WL-00009", MaterialName = "30%苯醚甲环唑水分散粒剂", CommonName = "苯醚甲环唑", BaseUnit = "克", SpecModel = "100g/袋", MaterialLevel = "一级", DosageForm = "水分散粒剂", CropSite = "果树、蔬菜、禾谷类作物", ControlTarget = "黑星病、白粉病、叶斑病、锈病", UsageTime = "发病初期", RegistrationNo = "PD20190123", ProductAttribute = "杀菌剂", CropAttribute = "经济作物", ProductManager = "王强", ProductInfo = "三唑类广谱杀菌剂，具有保护、治疗和铲除作用。对子囊菌、担子菌和半知菌引起的病害有特效。", Images = new List<string>{ DemoImages[0], DemoImages[3], DemoImages[2] } },
        new Material { Id = "WL-00010", MaterialName = "10%氰氟草酯乳油", CommonName = "氰氟草酯", BaseUnit = "升", SpecModel = "1L/瓶", MaterialLevel = "三级", DosageForm = "乳油", CropSite = "水稻田", ControlTarget = "稗草、千金子等禾本科杂草", UsageTime = "水稻插秧后5-7天", RegistrationNo = "PD20180456", ProductAttribute = "除草剂", CropAttribute = "水稻", ProductManager = "陈芳", ProductInfo = "芳氧苯氧丙酸酯类除草剂，用于水稻田防除禾本科杂草。对千金子、稗草特效。", Images = new List<string>{ DemoImages[2] } },
        new Material { Id = "WL-00011", MaterialName = "45%咪鲜胺水乳剂", CommonName = "咪鲜胺", BaseUnit = "毫升", SpecModel = "500ml/瓶", MaterialLevel = "一级", DosageForm = "水乳剂", CropSite = "果树、蔬菜、食用菌", ControlTarget = "炭疽病、蒂腐病、青霉病、绿霉病", UsageTime = "发病初期或采后处理", RegistrationNo = "PD20210234", ProductAttribute = "杀菌剂", CropAttribute = "经济作物", ProductManager = "刘洋", ProductInfo = "咪唑类广谱杀菌剂，对多种作物由子囊菌和半知菌引起的病害有明显防效。也可用于水果采后防腐保鲜。", Images = new List<string>() },
        new Material { Id = "WL-00012", MaterialName = "50%氯氰菊酯乳油", CommonName = "氯氰菊酯", BaseUnit = "毫升", SpecModel = "250ml/瓶", MaterialLevel = "二级", DosageForm = "乳油", CropSite = "蔬菜、果树、棉花、大豆", ControlTarget = "菜青虫、棉铃虫、食心虫、蚜虫", UsageTime = "低龄幼虫期", RegistrationNo = "PD20170890", ProductAttribute = "杀虫剂", CropAttribute = "大田作物", ProductManager = "赵敏", ProductInfo = "拟除虫菊酯类杀虫剂，具有触杀和胃毒作用。杀虫谱广，击倒速度快。", Images = new List<string>{ DemoImages[1], DemoImages[0] } }
    };

    public MaterialService(IOptions<KingdeeSettings> settings, KingdeeApiClient? kingdee, ILogger<MaterialService> logger)
    {
        _settings = settings.Value;
        _kingdee = kingdee;
        _logger = logger;
    }

    public async Task<ApiResponse<List<Material>>> GetMaterialsAsync(MaterialQueryParams query)
    {
        // 金蝶模式
        if (_settings.Enable && _kingdee != null)
        {
            try
            {
                _logger.LogInformation("调用金蝶API查询物料，关键词={Keyword}, 等级={Level}", query.Keyword, query.Level);

                // 先执行列表查询（不带筛选条件读取前1000条，再在内存过滤，简单且可靠）
                var rows = await _kingdee.ExecuteBillQueryAsync(
                    formId: "BD_MATERIAL",
                    fieldKeys: "FMATERIALID,FNUMBER,FNAME,FSPECIFICATION,FBaseUnitId__FNumber,FMATERIALGROUP__FNUMBER",
                    filterString: BuildKingdeeFilter(query.Keyword, query.Level),
                    orderBy: "FNumber ASC",
                    topRowCount: "1000"
                );

                if (rows == null)
                {
                    return new ApiResponse<List<Material>>
                    {
                        Success = false,
                        Message = "调用金蝶API失败，请检查配置",
                        Data = new List<Material>()
                    };
                }

                var list = new List<Material>();
                foreach (var row in rows)
                {
                    var m = KingdeeMaterialMapper.FromRow(row);
                    if (m != null) list.Add(m);
                }

                // 分页
                var totalCount = list.Count;
                var paged = list
                    .Skip((query.PageIndex - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToList();

                return new ApiResponse<List<Material>>
                {
                    Success = true,
                    Message = "查询成功（来自金蝶云星空）",
                    Data = paged,
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用金蝶API异常，降级使用Mock数据");
                // 失败回退到 Mock
                return MockQuery(query);
            }
        }

        // Mock 模式
        return MockQuery(query);
    }

    public async Task<ApiResponse<Material>> GetMaterialByIdAsync(string id)
    {
        if (_settings.Enable && _kingdee != null)
        {
            try
            {
                _logger.LogInformation("调用金蝶API查询物料详情：{Id}", id);

                // 先按列表/主键读出基础信息，再用 View 读详情和附件
                var rows = await _kingdee.ExecuteBillQueryAsync(
                    formId: "BD_MATERIAL",
                    fieldKeys: "FMATERIALID,FNUMBER,FNAME,FSPECIFICATION,FBaseUnitId__FNumber,FMATERIALGROUP__FNUMBER",
                    filterString: $"FMATERIALID='{id}' OR FNUMBER='{id}'",
                    orderBy: "FNumber ASC",
                    topRowCount: "1"
                );

                if (rows == null || rows.Count == 0)
                {
                    return new ApiResponse<Material>
                    {
                        Success = false,
                        Message = "未找到该物料",
                        Data = null
                    };
                }

                var material = KingdeeMaterialMapper.FromRow(rows[0]);
                if (material == null)
                {
                    return new ApiResponse<Material>
                    {
                        Success = false,
                        Message = "物料数据解析失败",
                        Data = null
                    };
                }

                // 用 View 接口读取详情和图片信息
                var viewResult = await _kingdee.ViewAsync("BD_MATERIAL", material.Id);
                KingdeeMaterialMapper.EnrichFromView(material, viewResult);
                material.Images = _kingdee.ExtractImageUrls(viewResult);

                return new ApiResponse<Material>
                {
                    Success = true,
                    Message = "查询成功（来自金蝶云星空）",
                    Data = material
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用金蝶API查询详情失败，降级使用Mock数据");
                var fallback = MockMaterials.FirstOrDefault(m => m.Id == id);
                if (fallback != null)
                {
                    return new ApiResponse<Material>
                    {
                        Success = true,
                        Message = "查询成功（Mock，金蝶API调用失败已回退）",
                        Data = fallback
                    };
                }
                return new ApiResponse<Material> { Success = false, Message = "未找到该物料" };
            }
        }

        var mockItem = MockMaterials.FirstOrDefault(m => m.Id == id);
        return new ApiResponse<Material>
        {
            Success = mockItem != null,
            Message = mockItem != null ? "查询成功" : "未找到该物料",
            Data = mockItem
        };
    }

    private ApiResponse<List<Material>> MockQuery(MaterialQueryParams query)
    {
        IEnumerable<Material> filtered = MockMaterials;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            filtered = filtered.Where(m =>
                (m.MaterialName ?? string.Empty).ToLower().Contains(kw) ||
                (m.CommonName ?? string.Empty).ToLower().Contains(kw) ||
                (m.SpecModel ?? string.Empty).ToLower().Contains(kw) ||
                (m.RegistrationNo ?? string.Empty).ToLower().Contains(kw) ||
                (m.ProductManager ?? string.Empty).ToLower().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            filtered = filtered.Where(m => m.MaterialLevel == query.Level);
        }

        var list = filtered.ToList();
        var totalCount = list.Count;
        var paged = list
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new ApiResponse<List<Material>>
        {
            Success = true,
            Message = "查询成功（Mock数据）",
            Data = paged,
            TotalCount = totalCount
        };
    }

    /// <summary>
    /// 构造金蝶过滤条件字符串
    /// 格式示例：FNAME LIKE '%草铵%' AND FMATERIALLEVEL='一级'
    /// </summary>
    private string BuildKingdeeFilter(string? keyword, string? level)
    {
        var conditions = new List<string>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            conditions.Add(
                $"(FNAME LIKE '%{kw}%' OR FSPECIFICATION LIKE '%{kw}%' OR FNUMBER LIKE '%{kw}%')"
            );
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            // 物料等级可能使用基础资料/枚举，这里用模糊匹配
            conditions.Add($"FMaterialGroup.FNumber LIKE '%{level}%'");
        }

        return conditions.Count == 0 ? string.Empty : string.Join(" AND ", conditions);
    }
}

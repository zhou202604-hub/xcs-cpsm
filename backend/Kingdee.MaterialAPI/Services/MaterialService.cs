using Kingdee.MaterialAPI.Models;

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
/// 物料服务实现（含Mock数据，后续可接入金蝶API）
/// </summary>
public class MaterialService : IMaterialService
{
    private static readonly List<Material> MockMaterials = new()
    {
        new Material { Id = "WL-00001", MaterialName = "20%草铵膦水剂", CommonName = "草铵膦", BaseUnit = "升", SpecModel = "5L/桶", MaterialLevel = "一级", DosageForm = "水剂", CropSite = "果园、非耕地", ControlTarget = "牛筋草、狗尾草、马唐等一年生杂草", UsageTime = "杂草3-5叶期", RegistrationNo = "PD20200012", ProductAttribute = "除草剂", CropAttribute = "大田作物", ProductManager = "张伟", ProductInfo = "本产品为触杀型除草剂，对多种一年生和多年生杂草有良好防效。" },
        new Material { Id = "WL-00002", MaterialName = "40%多菌灵悬浮剂", CommonName = "多菌灵", BaseUnit = "千克", SpecModel = "1kg/袋", MaterialLevel = "一级", DosageForm = "悬浮剂", CropSite = "蔬菜、果树、水稻", ControlTarget = "稻瘟病、纹枯病、白粉病、炭疽病", UsageTime = "发病初期", RegistrationNo = "PD20180345", ProductAttribute = "杀菌剂", CropAttribute = "经济作物", ProductManager = "李娜", ProductInfo = "广谱性杀菌剂，具有保护和治疗作用。" },
        new Material { Id = "WL-00003", MaterialName = "25%吡虫啉可湿性粉剂", CommonName = "吡虫啉", BaseUnit = "克", SpecModel = "100g/袋", MaterialLevel = "二级", DosageForm = "可湿性粉剂", CropSite = "小麦、水稻、蔬菜、果树", ControlTarget = "蚜虫、飞虱、蓟马、粉虱", UsageTime = "害虫发生初期", RegistrationNo = "PD20190789", ProductAttribute = "杀虫剂", CropAttribute = "大田作物", ProductManager = "王强", ProductInfo = "烟碱类杀虫剂，对刺吸式口器害虫有特效。" },
        new Material { Id = "WL-00004", MaterialName = "5%阿维菌素乳油", CommonName = "阿维菌素", BaseUnit = "毫升", SpecModel = "500ml/瓶", MaterialLevel = "二级", DosageForm = "乳油", CropSite = "蔬菜、果树、棉花", ControlTarget = "红蜘蛛、斑潜蝇、菜青虫、棉铃虫", UsageTime = "低龄幼虫期", RegistrationNo = "PD20170567", ProductAttribute = "杀虫剂", CropAttribute = "经济作物", ProductManager = "陈芳", ProductInfo = "生物源杀虫剂，对螨类和鳞翅目幼虫有良好防效。" },
        new Material { Id = "WL-00005", MaterialName = "95%草甘膦原药", CommonName = "草甘膦", BaseUnit = "千克", SpecModel = "25kg/袋", MaterialLevel = "三级", DosageForm = "原药", CropSite = "非耕地、果园行间", ControlTarget = "一年生及多年生杂草", UsageTime = "杂草生长旺盛期", RegistrationNo = "PD20150234", ProductAttribute = "除草剂", CropAttribute = "非耕地", ProductManager = "刘洋", ProductInfo = "广谱灭生性除草剂，用于非耕地除草效果显著。" },
        new Material { Id = "WL-00006", MaterialName = "15%氟磺胺草醚乳油", CommonName = "氟磺胺草醚", BaseUnit = "升", SpecModel = "1L/瓶", MaterialLevel = "二级", DosageForm = "乳油", CropSite = "大豆田、花生田", ControlTarget = "反枝苋、马齿苋、藜等阔叶杂草", UsageTime = "大豆2-4片复叶期", RegistrationNo = "PD20210456", ProductAttribute = "除草剂", CropAttribute = "豆科作物", ProductManager = "赵敏", ProductInfo = "二苯醚类选择性苗后除草剂。" },
        new Material { Id = "WL-00007", MaterialName = "80%代森锰锌可湿性粉剂", CommonName = "代森锰锌", BaseUnit = "千克", SpecModel = "2kg/袋", MaterialLevel = "一级", DosageForm = "可湿性粉剂", CropSite = "果树、蔬菜、大田作物", ControlTarget = "霜霉病、疫病、炭疽病、叶斑病", UsageTime = "发病前或发病初期", RegistrationNo = "PD20160890", ProductAttribute = "杀菌剂", CropAttribute = "经济作物", ProductManager = "张伟", ProductInfo = "广谱保护性杀菌剂，含锰、锌微量元素。" },
        new Material { Id = "WL-00008", MaterialName = "20%噻虫嗪水分散粒剂", CommonName = "噻虫嗪", BaseUnit = "克", SpecModel = "200g/袋", MaterialLevel = "二级", DosageForm = "水分散粒剂", CropSite = "水稻、小麦、棉花、蔬菜", ControlTarget = "稻飞虱、蚜虫、蓟马、白粉虱", UsageTime = "害虫发生初期", RegistrationNo = "PD20200678", ProductAttribute = "杀虫剂", CropAttribute = "大田作物", ProductManager = "李娜", ProductInfo = "新一代烟碱类杀虫剂，杀虫谱广、活性高。" },
        new Material { Id = "WL-00009", MaterialName = "30%苯醚甲环唑水分散粒剂", CommonName = "苯醚甲环唑", BaseUnit = "克", SpecModel = "100g/袋", MaterialLevel = "一级", DosageForm = "水分散粒剂", CropSite = "果树、蔬菜、禾谷类作物", ControlTarget = "黑星病、白粉病、叶斑病、锈病", UsageTime = "发病初期", RegistrationNo = "PD20190123", ProductAttribute = "杀菌剂", CropAttribute = "经济作物", ProductManager = "王强", ProductInfo = "三唑类广谱杀菌剂，具有保护、治疗和铲除作用。" },
        new Material { Id = "WL-00010", MaterialName = "10%氰氟草酯乳油", CommonName = "氰氟草酯", BaseUnit = "升", SpecModel = "1L/瓶", MaterialLevel = "三级", DosageForm = "乳油", CropSite = "水稻田", ControlTarget = "稗草、千金子等禾本科杂草", UsageTime = "水稻插秧后5-7天", RegistrationNo = "PD20180456", ProductAttribute = "除草剂", CropAttribute = "水稻", ProductManager = "陈芳", ProductInfo = "芳氧苯氧丙酸酯类除草剂，对千金子、稗草特效。" },
        new Material { Id = "WL-00011", MaterialName = "45%咪鲜胺水乳剂", CommonName = "咪鲜胺", BaseUnit = "毫升", SpecModel = "500ml/瓶", MaterialLevel = "一级", DosageForm = "水乳剂", CropSite = "果树、蔬菜、食用菌", ControlTarget = "炭疽病、蒂腐病、青霉病、绿霉病", UsageTime = "发病初期或采后处理", RegistrationNo = "PD20210234", ProductAttribute = "杀菌剂", CropAttribute = "经济作物", ProductManager = "刘洋", ProductInfo = "咪唑类广谱杀菌剂，也可用于水果采后防腐保鲜。" },
        new Material { Id = "WL-00012", MaterialName = "50%氯氰菊酯乳油", CommonName = "氯氰菊酯", BaseUnit = "毫升", SpecModel = "250ml/瓶", MaterialLevel = "二级", DosageForm = "乳油", CropSite = "蔬菜、果树、棉花、大豆", ControlTarget = "菜青虫、棉铃虫、食心虫、蚜虫", UsageTime = "低龄幼虫期", RegistrationNo = "PD20170890", ProductAttribute = "杀虫剂", CropAttribute = "大田作物", ProductManager = "赵敏", ProductInfo = "拟除虫菊酯类杀虫剂，杀虫谱广，击倒速度快。" }
    };

    public async Task<ApiResponse<List<Material>>> GetMaterialsAsync(MaterialQueryParams query)
    {
        await Task.Delay(100);

        var filtered = MockMaterials.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            filtered = filtered.Where(m =>
                m.MaterialName.ToLower().Contains(kw) ||
                m.CommonName.ToLower().Contains(kw) ||
                m.SpecModel.ToLower().Contains(kw) ||
                m.RegistrationNo.ToLower().Contains(kw) ||
                m.ProductManager.ToLower().Contains(kw));
        }

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            filtered = filtered.Where(m => m.MaterialLevel == query.Level);
        }

        var totalCount = filtered.Count();
        var paged = filtered
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new ApiResponse<List<Material>>
        {
            Success = true,
            Message = "查询成功",
            Data = paged,
            TotalCount = totalCount
        };
    }

    public async Task<ApiResponse<Material>> GetMaterialByIdAsync(string id)
    {
        await Task.Delay(50);

        var material = MockMaterials.FirstOrDefault(m => m.Id == id);

        if (material == null)
        {
            return new ApiResponse<Material>
            {
                Success = false,
                Message = "未找到该物料",
                Data = null
            };
        }

        return new ApiResponse<Material>
        {
            Success = true,
            Message = "查询成功",
            Data = material
        };
    }
}

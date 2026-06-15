using Kingdee.MaterialAPI.Models;

namespace Kingdee.MaterialAPI.Services;

public interface IMaterialService
{
    Task<ApiResponse<List<Material>>> QueryListAsync(MaterialQueryParams q);
    Task<ApiResponse<Material>> GetByIdAsync(string id);
}

/// <summary>
/// 物料业务服务：
/// - 启用金蝶时调用金蝶 Web API
/// - 未启用或调用失败时降级返回内置示例数据
/// </summary>
public class MaterialService : IMaterialService
{
    private readonly AppConfigStore _configStore;
    private readonly KingdeeApiClient _kingdee;
    private readonly KingdeeMaterialMapper _mapper;
    private readonly ILogger<MaterialService> _logger;

    public MaterialService(AppConfigStore configStore, KingdeeApiClient kingdee, KingdeeMaterialMapper mapper, ILogger<MaterialService> logger)
    {
        _configStore = configStore;
        _kingdee = kingdee;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ApiResponse<List<Material>>> QueryListAsync(MaterialQueryParams q)
    {
        var cfg = _configStore.Get();
        if (cfg.Kingdee.Enable)
        {
            try
            {
                var select = _mapper.BuildSelectFields();
                var selectFields = select.Split(',', StringSplitOptions.RemoveEmptyEntries);

                // 根据字段映射生成 WHERE
                var filter = BuildFilter(q, cfg.FieldMappings);
                var rows = await _kingdee.ExecuteBillQueryAsync(select, filter, "FMATERIALID DESC", q.PageSize);
                var list = new List<Material>();
                if (rows != null)
                {
                    foreach (var r in rows)
                        list.Add(_mapper.FromRow(r, selectFields));
                }
                _logger.LogInformation("金蝶物料查询返回 {Count} 条（关键词: {Kw})", list.Count, q.Keyword);
                return new ApiResponse<List<Material>> { Success = true, Message = "ok", Data = list };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用金蝶 API 失败，降级使用 Mock 数据");
                return MockResponse(q, ex.Message);
            }
        }
        return MockResponse(q, null);
    }

    public async Task<ApiResponse<Material>> GetByIdAsync(string id)
    {
        var cfg = _configStore.Get();
        if (cfg.Kingdee.Enable)
        {
            try
            {
                var select = _mapper.BuildSelectFields();
                var selectFields = select.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var rows = await _kingdee.ExecuteBillQueryAsync(
                    select, $"FMATERIALID='{id.Replace("'", "''")}'", "", 1);
                if (rows != null && rows.Count > 0)
                {
                    var mat = _mapper.FromRow(rows[0], selectFields);
                    var view = await _kingdee.ViewAsync(id);
                    mat.Images = _mapper.ExtractImageUrls(view, id);
                    return new ApiResponse<Material> { Success = true, Message = "ok", Data = mat };
                }
                return new ApiResponse<Material> { Success = false, Message = "未找到" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetById 金蝶异常");
            }
        }
        var mock = MockResponse(q: null, null).Data?.FirstOrDefault(m =>
            m.MaterialId == id || m.Number == id);
        return new ApiResponse<Material>
        {
            Success = mock != null,
            Data = mock,
            Message = mock != null ? "ok" : "notfound"
        };
    }

    private string BuildFilter(MaterialQueryParams q, List<FieldMappingItem> mps)
    {
        // 关键字模糊查询：只对启用的、非图片字段做 LIKE
        var likes = new List<string>();
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            foreach (var fm in mps.Where(f => f.Enabled && f.FieldKey != "images" && !string.IsNullOrWhiteSpace(f.KingdeeField)))
            {
                var firstField = fm.KingdeeField.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                if (string.IsNullOrWhiteSpace(firstField)) continue;
                likes.Add($"{firstField} LIKE '%{q.Keyword.Replace("'", "''")}%'");
                if (likes.Count >= 10) break; // 避免子句过长
            }
        }
        // 等级过滤
        string? levelClause = null;
        if (!string.IsNullOrWhiteSpace(q.Level))
        {
            var levelFm = mps.FirstOrDefault(f => f.FieldKey.Equals("materialLevel", StringComparison.OrdinalIgnoreCase));
            var fieldName = levelFm?.KingdeeField?.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                levelClause = $"{fieldName} = '{q.Level.Replace("'", "''")}'";
            }
        }

        if (likes.Count > 0 && levelClause != null)
            return $"({string.Join(" OR ", likes)}) AND {levelClause}";
        if (likes.Count > 0) return string.Join(" OR ", likes);
        if (levelClause != null) return levelClause;
        return "";
    }

    private ApiResponse<List<Material>> MockResponse(MaterialQueryParams? q, string? msg)
    {
        var materials = new List<Material>
        {
            new Material { MaterialId = "1", Number = "P001", MaterialName = "25% 噻虫嗪悬浮剂", GeneralName = "噻虫嗪", BasicUnit = "瓶", Specification = "100ml/瓶", MaterialLevel = "一级", RegistrationForm = "悬浮剂", CropPlace = "水稻田", ControlTarget = "蚜虫、稻飞虱", UseTime = "5-9月", RegistrationNo = "PD20150001", ProductAttribute = "杀虫剂", ProductManager = "李经理" },
            new Material { MaterialId = "2", Number = "P002", MaterialName = "40% 戊唑醇水乳剂", GeneralName = "戊唑醇", BasicUnit = "瓶", Specification = "500ml/瓶", MaterialLevel = "二级", RegistrationForm = "水乳剂", CropPlace = "小麦田", ControlTarget = "赤霉病、白粉病", UseTime = "4-7月", RegistrationNo = "PD20140020", ProductAttribute = "杀菌剂", ProductManager = "王经理",
                Images = new List<string> { "https://picsum.photos/seed/mock002a/600/400", "https://picsum.photos/seed/mock002b/600/400" } },
            new Material { MaterialId = "3", Number = "P003", MaterialName = "草甘膦水剂", GeneralName = "草甘膦", BasicUnit = "桶", Specification = "5L/桶", MaterialLevel = "三级", RegistrationForm = "水剂", CropPlace = "果园/非耕地", ControlTarget = "一年生、多年生杂草", UseTime = "全年", RegistrationNo = "PD85155", ProductAttribute = "除草剂", ProductManager = "赵经理" },
            new Material { MaterialId = "4", Number = "P004", MaterialName = "氯氰菊酯乳油", GeneralName = "氯氰菊酯", BasicUnit = "瓶", Specification = "250ml/瓶", MaterialLevel = "一级", RegistrationForm = "乳油", CropPlace = "蔬菜/果树", ControlTarget = "菜青虫、蚜虫", UseTime = "3-10月", RegistrationNo = "PD20100100", ProductAttribute = "杀虫剂", ProductManager = "孙经理", Images = new List<string> { "https://picsum.photos/seed/mock004/600/400" } },
            new Material { MaterialId = "5", Number = "P005", MaterialName = "多菌灵可湿性粉剂", GeneralName = "多菌灵", BasicUnit = "袋", Specification = "500g/袋", MaterialLevel = "二级", RegistrationForm = "可湿性粉剂", CropPlace = "多种作物", ControlTarget = "真菌病害", UseTime = "4-10月", RegistrationNo = "PD20050011", ProductAttribute = "杀菌剂", ProductManager = "周经理" }
        };

        IEnumerable<Material> result = materials;
        if (q != null)
        {
            if (!string.IsNullOrWhiteSpace(q.Keyword))
            {
                var kw = q.Keyword.Trim().ToLower();
                result = result.Where(m =>
                    (m.MaterialName ?? "").ToLower().Contains(kw) ||
                    (m.GeneralName ?? "").ToLower().Contains(kw) ||
                    (m.Specification ?? "").ToLower().Contains(kw) ||
                    (m.RegistrationNo ?? "").ToLower().Contains(kw) ||
                    (m.ProductManager ?? "").ToLower().Contains(kw));
            }
            if (!string.IsNullOrWhiteSpace(q.Level))
                result = result.Where(m => (m.MaterialLevel ?? "") == q.Level);
        }
        var data = result.ToList();
        return new ApiResponse<List<Material>>
        {
            Success = true,
            Message = msg != null ? "金蝶不可用，已降级为示例数据: " + msg : "示例数据（未启用金蝶）",
            Data = data
        };
    }
}

using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;

namespace Kingdee.MaterialAPI;

/// <summary>
/// 物料数据服务
/// - KingdeeEnable=true 时调用金蝶 API
/// - 否则返回 mock 数据
/// </summary>
public interface IMaterialService
{
    Task<ApiResponse<List<Material>>> QueryList(MaterialQueryParams q);
    Task<ApiResponse<Material>> GetById(string id);
}

public class MaterialService : IMaterialService
{
    private readonly AppConfigStore _config;
    private readonly KingdeeApiClient _kingdee;
    private readonly KingdeeMaterialMapper _mapper;
    private readonly ILogger<MaterialService> _logger;

    public MaterialService(AppConfigStore config, KingdeeApiClient kingdee, KingdeeMaterialMapper mapper, ILogger<MaterialService> logger)
    {
        _config = config;
        _kingdee = kingdee;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ApiResponse<List<Material>>> QueryList(MaterialQueryParams q)
    {
        var cfg = _config.Get();

        // ========= 金蝶模式 =========
        if (cfg.KingdeeEnable)
        {
            try
            {
                var formId = cfg.KingdeeMaterialFormId;
                var selectFields = _mapper.BuildSelectFields();

                // 组装查询条件（按字段映射中的 KingdeeField 模糊匹配）
                var enabledFields = cfg.FieldMappings
                    .Where(f => f.Enabled && !string.IsNullOrWhiteSpace(f.KingdeeField))
                    .SelectMany(f => f.KingdeeField.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var filter = "";
                if (!string.IsNullOrWhiteSpace(q.Keyword))
                {
                    var clauses = new List<string>();
                    foreach (var f in enabledFields.Take(6)) // 避免过长 where 子句
                    {
                        clauses.Add($"{f} like '%{q.Keyword.Replace("'", "''")}%'");
                    }
                    filter = string.Join(" OR ", clauses);
                }
                // 等级过滤（按"物料等级"字段等值匹配）
                if (!string.IsNullOrWhiteSpace(q.Level))
                {
                    var levelField = cfg.FieldMappings.FirstOrDefault(f =>
                        f.FieldKey.Equals("materialLevel", StringComparison.OrdinalIgnoreCase));
                    if (levelField != null && !string.IsNullOrWhiteSpace(levelField.KingdeeField))
                    {
                        var levelWhere = $"{levelField.KingdeeField} = '{q.Level.Replace("'", "''")}'";
                        filter = string.IsNullOrWhiteSpace(filter) ? levelWhere : $"({filter}) AND {levelWhere}";
                    }
                }

                var rows = await _kingdee.ExecuteBillQueryAsync(formId, selectFields, filter, "FMATERIALID desc", q.PageSize);
                var list = new List<Material>();
                if (rows != null)
                {
                    foreach (var row in rows)
                        list.Add(_mapper.FromRow(row));
                }
                _logger.LogInformation("金蝶物料查询返回 {Count} 条（关键词={Kw})", list.Count, q.Keyword);
                return new ApiResponse<List<Material>> { Success = true, Message = "ok", Data = list };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "调用金蝶 API 异常，降级走 Mock");
                return MockQuery(q);
            }
        }

        // ========= 降级 Mock 数据 =========
        return MockQuery(q);
    }

    public async Task<ApiResponse<Material>> GetById(string id)
    {
        var cfg = _config.Get();
        if (cfg.KingdeeEnable)
        {
            try
            {
                // 先查 View 取详情（含附件/图片）
                var formId = cfg.KingdeeMaterialFormId;
                var view = await _kingdee.ViewAsync(formId, id);

                // 再查 ExecuteBillQuery 取基础字段（也可以直接用 View 返回的基础字段）
                var rows = await _kingdee.ExecuteBillQueryAsync(formId, _mapper.BuildSelectFields(),
                    $"FMATERIALID='{id.Replace("'", "''")}'", "", 1);

                if (rows != null && rows.Count > 0)
                {
                    var mat = _mapper.FromRow(rows[0]);
                    if (view != null) _mapper.EnrichFromView(mat, view);
                    return new ApiResponse<Material> { Success = true, Message = "ok", Data = mat };
                }
                return new ApiResponse<Material> { Success = false, Message = "未找到" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetById 金蝶异常");
                var mock = MockQuery(new MaterialQueryParams { PageSize = 1 }).Data?.FirstOrDefault(m => m.MaterialId == id);
                return new ApiResponse<Material> { Success = mock != null, Data = mock, Message = mock != null ? "ok" : "notfound" };
            }
        }

        var list = MockQuery(new MaterialQueryParams { PageSize = 50 }).Data;
        var found = list?.FirstOrDefault(m => m.MaterialId == id || m.Number == id);
        return new ApiResponse<Material> { Success = found != null, Data = found, Message = found != null ? "ok" : "notfound" };
    }

    private ApiResponse<List<Material>> MockQuery(MaterialQueryParams q)
    {
        // 简单的 Mock 数据（农药示例）—— 真实环境请关闭此分支由金蝶 API 替代
        var baseList = new List<Material>
        {
            new () { MaterialId = "1", Number = "P001", MaterialName = "25% 噻虫嗪悬浮剂", GeneralName = "噻虫嗪", BasicUnit = "瓶", Specification = "100ml/瓶", MaterialLevel = "一级", RegistrationForm = "悬浮剂", CropPlace = "水稻田", ControlTarget = "蚜虫、稻飞虱", UseTime = "5-9月", RegistrationNo = "PD20150001", ProductAttribute = "杀虫剂", CropAttribute = "大田作物", ProductManager = "李经理", ProductInfo = "高效、低毒，适用于综合防治" },
            new () { MaterialId = "2", Number = "P002", MaterialName = "40% 戊唑醇水乳剂", GeneralName = "戊唑醇", BasicUnit = "瓶", Specification = "500ml/瓶", MaterialLevel = "二级", RegistrationForm = "水乳剂", CropPlace = "小麦田", ControlTarget = "赤霉病、白粉病", UseTime = "4-7月", RegistrationNo = "PD20140020", ProductAttribute = "杀菌剂", CropAttribute = "大田作物", ProductManager = "王经理", ProductInfo = "三唑类杀菌剂，防治真菌病害", Images = new List<string>{ "https://picsum.photos/seed/mock002", "https://picsum.photos/seed/mock002b"} },
            new () { MaterialId = "3", Number = "P003", MaterialName = "草甘膦水剂", GeneralName = "草甘膦", BasicUnit = "桶", Specification = "5L/桶", MaterialLevel = "三级", RegistrationForm = "水剂", CropPlace = "果园/非耕地", ControlTarget = "一年生、多年生杂草", UseTime = "全年", RegistrationNo = "PD85155", ProductAttribute = "除草剂", ProductManager = "赵经理" },
            new () { MaterialId = "4", Number = "P004", MaterialName = "氯氰菊酯乳油", GeneralName = "氯氰菊酯", BasicUnit = "瓶", Specification = "250ml/瓶", MaterialLevel = "一级", RegistrationForm = "乳油", CropPlace = "蔬菜/果树", ControlTarget = "菜青虫、蚜虫", UseTime = "3-10月", RegistrationNo = "PD20100100", ProductAttribute = "杀虫剂", ProductManager = "孙经理", Images = new List<string>{ "https://picsum.photos/seed/mock004"} },
            new () { MaterialId = "5", Number = "P005", MaterialName = "多菌灵可湿性粉剂", GeneralName = "多菌灵", BasicUnit = "袋", Specification = "500g/袋", MaterialLevel = "二级", RegistrationForm = "可湿性粉剂", CropPlace = "多种作物", ControlTarget = "真菌病害", UseTime = "4-10月", RegistrationNo = "PD20050011", ProductAttribute = "杀菌剂", ProductManager = "周经理" }
        };

        IEnumerable<Material> result = baseList;
        if (!string.IsNullOrWhiteSpace(q.Keyword))
        {
            var kw = q.Keyword.ToLower();
            result = result.Where(m =>
                (m.MaterialName ?? "").ToLower().Contains(kw) ||
                (m.GeneralName ?? "").ToLower().Contains(kw) ||
                (m.Specification ?? "").ToLower().Contains(kw) ||
                (m.RegistrationNo ?? "").ToLower().Contains(kw) ||
                (m.ProductManager ?? "").ToLower().Contains(kw));
        }
        if (!string.IsNullOrWhiteSpace(q.Level))
            result = result.Where(m => (m.MaterialLevel ?? "") == q.Level);

        var page = result.Skip((q.PageIndex - 1) * q.PageSize).Take(q.PageSize).ToList();
        return new ApiResponse<List<Material>> { Success = true, Message = "ok", Data = page };
    }
}

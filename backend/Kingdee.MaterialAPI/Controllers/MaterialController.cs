using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kingdee.MaterialAPI.Controllers;

/// <summary>
/// 物料相关 API —— 移动端页面调用
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MaterialController : ControllerBase
{
    private readonly IMaterialService _svc;
    private readonly ILogger<MaterialController> _logger;

    public MaterialController(IMaterialService svc, ILogger<MaterialController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ApiResponse<List<Material>>> List([FromQuery] string keyword = "",
        [FromQuery] string level = "", [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 50)
    {
        _logger.LogInformation("查询物料：keyword={K}, level={L}", keyword, level);
        var result = await _svc.QueryListAsync(new MaterialQueryParams
        {
            Keyword = keyword,
            Level = level,
            PageIndex = pageIndex,
            PageSize = pageSize
        });
        return result;
    }

    [HttpGet("{id}")]
    public async Task<ApiResponse<Material>> Get(string id)
    {
        return await _svc.GetByIdAsync(id);
    }
}

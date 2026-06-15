using Microsoft.AspNetCore.Mvc;
using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;

namespace Kingdee.MaterialAPI.Controllers;

/// <summary>
/// 物料查询控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MaterialController : ControllerBase
{
    private readonly IMaterialService _materialService;

    public MaterialController(IMaterialService materialService)
    {
        _materialService = materialService;
    }

    /// <summary>
    /// 查询物料列表
    /// </summary>
    /// <param name="keyword">搜索关键词（物料名称/通用名/规格型号/登记证号/产品经理）</param>
    /// <param name="level">物料等级筛选（一级/二级/三级）</param>
    /// <param name="pageIndex">页码，默认1</param>
    /// <param name="pageSize">每页数量，默认20</param>
    /// <returns>物料列表</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<Material>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMaterials(
        [FromQuery] string? keyword = null,
        [FromQuery] string? level = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new MaterialQueryParams
        {
            Keyword = keyword,
            Level = level,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        var result = await _materialService.GetMaterialsAsync(query);
        return Ok(result);
    }

    /// <summary>
    /// 根据ID获取物料详情
    /// </summary>
    /// <param name="id">物料编号</param>
    /// <returns>物料详细信息</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<Material>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMaterialById(string id)
    {
        var result = await _materialService.GetMaterialByIdAsync(id);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }
}

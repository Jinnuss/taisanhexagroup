using Dapper;
using Hexa.Api.Models;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hexa.Api.Controllers;
[ApiController,Route("api/assets/{id:int}/software"),Authorize]
public class SoftwareController(Database db,AssetService assets):ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(int id)
    {await assets.Get(id);await using var c=await db.Open();return Ok(await c.QueryAsync("SELECT * FROM dbo.InstalledSoftware WHERE AssetId=@id ORDER BY SoftwareName",new {id}));}
    [HttpPost,Authorize(Roles="admin")]
    public async Task<IActionResult> Create(int id,SoftwareInput x)
    {
        var asset=await assets.Get(id);
        Rules.Require(new[]{"LAPTOP","PC","DESKTOP"}.Contains(asset.CategoryCode?.ToUpperInvariant()),"Chỉ quản lý phần mềm trên Laptop/PC.");
        await using var c=await db.Open();var p=new DynamicParameters(x);p.Add("id",id);
        return StatusCode(201,await c.QuerySingleAsync("""
            INSERT dbo.InstalledSoftware(AssetId,SoftwareName,Version,Publisher,InstalledDate,Notes)
            OUTPUT INSERTED.* VALUES(@id,@SoftwareName,@Version,@Publisher,@InstalledDate,@Notes)
            """,p));
    }
    [HttpDelete("{softwareId:int}"),Authorize(Roles="admin")]
    public async Task<IActionResult> Delete(int id,int softwareId)
    {
        await using var c=await db.Open();var n=await c.ExecuteAsync("DELETE dbo.InstalledSoftware WHERE Id=@softwareId AND AssetId=@id",new {id,softwareId});
        Rules.Require(n>0,"Không tìm thấy phần mềm.",404);return Ok(new {message="Đã xóa phần mềm."});
    }
}

using Hexa.Api.Models;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Hexa.Api.Controllers;
[ApiController,Route("api/assets"),Authorize]
public class AssetsController(AssetService assets):ControllerBase
{
    [HttpGet] public async Task<IActionResult> List([FromQuery] AssetFilter filter)=>Ok(await assets.List(filter));
    [HttpGet("stats")] public async Task<IActionResult> Stats()=>Ok(await assets.Stats());
    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id)=>Ok(await assets.Get(id));
    [HttpPost,Authorize(Roles="admin")] public async Task<IActionResult> Create(AssetInput input)=>StatusCode(201,await assets.Create(input));
    [HttpPut("{id:int}"),Authorize(Roles="admin")] public async Task<IActionResult> Update(int id,AssetInput input)=>Ok(await assets.Update(id,input));
    [HttpDelete("{id:int}"),Authorize(Roles="admin")] public async Task<IActionResult> Delete(int id) {await assets.Delete(id);return Ok(new {message="Xóa tài sản thành công."});}
}

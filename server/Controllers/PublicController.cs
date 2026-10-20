using Dapper;
using Hexa.Api.Models;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
namespace Hexa.Api.Controllers;
[ApiController]
public class PublicController(Database db,AssetService assets,DetailService details,IConfiguration config):ControllerBase
{
    [HttpGet("api/assets/{id:int}/qr"),Authorize]
    public async Task<IActionResult> Qr(int id)
    {
        var a=await assets.Get(id);var url=$"{config["PublicAppUrl"]!.TrimEnd('/')}/?asset={a.PublicId}";
        using var generator=new QRCodeGenerator();using var data=generator.CreateQrCode(url,QRCodeGenerator.ECCLevel.M);
        using var qr=new PngByteQRCode(data);var bytes=qr.GetGraphic(12);
        return Ok(new {publicUrl=url,qrDataUrl="data:image/png;base64,"+Convert.ToBase64String(bytes)});
    }
    // V8 public QR deliberately retains anonymous detail lookup using the existing unguessable PublicId.
    [HttpGet("api/public/assets/{publicId:guid}"),AllowAnonymous]
    public async Task<IActionResult> Asset(Guid publicId)
    {
        await using var c=await db.Open();
        var a=await c.QuerySingleOrDefaultAsync<AssetRow>(AssetService.SelectSql+" WHERE a.PublicId=@publicId",new {publicId})
            ?? throw new ApiException(404,"Không tìm thấy tài sản.");
        var assignments=await c.QueryAsync("SELECT AssignedToSnapshot,AssignedDate,ReturnedDate,IsActive FROM dbo.AssetAssignments WHERE AssetId=@id ORDER BY Id DESC",new {id=a.Id});
        var software=await c.QueryAsync("SELECT SoftwareName,Version,Publisher,InstalledDate FROM dbo.InstalledSoftware WHERE AssetId=@id ORDER BY SoftwareName",new {id=a.Id});
        var images=await details.Images(a);
        // Public page displays inventory information, not account data or administrative history notes.
        var asset=new {a.Id,a.PublicId,a.AssetCode,a.AssetName,a.CategoryCode,a.CategoryName,a.DepartmentCode,
            a.DepartmentName,a.AssignedTo,a.IpAddress,a.SerialNumber,a.PurchaseDate,a.Status};
        return Ok(new {asset,assignments,software,images});
    }
    [HttpGet("api/public/assets/{publicId:guid}/images/{imageId:int}"),AllowAnonymous]
    public async Task<IActionResult> Image(Guid publicId,int imageId)
    {
        await using var c=await db.Open();
        var image=await c.QuerySingleOrDefaultAsync("""
            SELECT i.MimeType,i.ImageData FROM dbo.AssetImages i JOIN dbo.Assets a ON a.Id=i.AssetId
            WHERE a.PublicId=@publicId AND i.Id=@imageId
            """,new {publicId,imageId});
        if(image is null) return NotFound();Response.Headers["X-Content-Type-Options"]="nosniff";
        Response.Headers.CacheControl="public,max-age=3600";
        return File((byte[])image.ImageData,(string)image.MimeType);
    }
}

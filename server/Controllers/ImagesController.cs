using System.Data;
using Dapper;
using Hexa.Api.Models;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
namespace Hexa.Api.Controllers;
[ApiController,Route("api/assets/{id:int}/images"),Authorize]
public class ImagesController(Database db,AssetService assets,DetailService details,CurrentUser user):ControllerBase
{
    [HttpGet] public async Task<IActionResult> List(int id)=>Ok(await details.Images(await assets.Get(id)));
    public static bool ValidImage(byte[] data,string mime) => mime switch {
        "image/png"=>data.Length>=8 && data.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),
        "image/jpeg"=>data.Length>=3 && data[0]==255 && data[1]==216 && data[2]==255,
        "image/webp"=>data.Length>=12 && System.Text.Encoding.ASCII.GetString(data,0,4)=="RIFF" && System.Text.Encoding.ASCII.GetString(data,8,4)=="WEBP",
        _=>false
    };
    [HttpPost,Authorize(Roles="admin"),RequestSizeLimit(35*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=35*1024*1024)]
    public async Task<IActionResult> Upload(int id,[FromForm] List<IFormFile> images)
    {
        Rules.Require(images.Count is >0 and <=6,"Chọn từ 1 đến 6 ảnh.");
        var buffers=new List<(string Name,string Mime,byte[] Bytes)>();
        foreach(var file in images) {
            Rules.Require(file.Length is >0 and <=5242880,"Mỗi ảnh phải nhỏ hơn hoặc bằng 5 MB.");
            var name=Path.GetFileName(file.FileName);Rules.Require(name.Length<=255,"Tên ảnh tối đa 255 ký tự.");
            using var ms=new MemoryStream();await file.CopyToAsync(ms);var bytes=ms.ToArray();
            Rules.Require(ValidImage(bytes,file.ContentType),"Chỉ chấp nhận dữ liệu ảnh JPG, PNG, WebP hợp lệ.");
            buffers.Add((name,file.ContentType,bytes));
        }
        await using var c=await db.Open();await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        var asset=await c.QuerySingleOrDefaultAsync<AssetRow>("SELECT * FROM dbo.Assets WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new {id},tx)
            ?? throw new ApiException(404,"Không tìm thấy tài sản.");
        int count=await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AssetImages WHERE AssetId=@id",new {id},tx);
        Rules.Require(count+images.Count<=6,"Mỗi tài sản chỉ được lưu tối đa 6 ảnh.");
        foreach(var b in buffers) {
            await c.ExecuteAsync("""
                INSERT dbo.AssetImages(AssetId,FileName,MimeType,FileSize,ImageData,IsPrimary,UploadedBy)
                VALUES(@id,@name,@mime,@size,@bytes,@primary,@actor)
                """,new {id,name=b.Name,mime=b.Mime,size=b.Bytes.Length,bytes=b.Bytes,primary=count==0,actor=user.Id},tx);count++;
        }
        await AssetService.Audit(c,tx,id,asset.AssetCode,"UPDATE","Tải ảnh tài sản",user.Id,null,new {count=images.Count});
        await tx.CommitAsync();return StatusCode(201,new {message=$"Đã tải {images.Count} ảnh."});
    }
    [HttpPut("{imageId:int}/primary"),Authorize(Roles="admin")]
    public async Task<IActionResult> Primary(int id,int imageId)
    {
        await using var c=await db.Open();await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        await c.ExecuteScalarAsync<int?>("SELECT Id FROM dbo.Assets WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new {id},tx);
        Rules.Require(await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.AssetImages WHERE Id=@imageId AND AssetId=@id",new {id,imageId},tx)==1,"Không tìm thấy ảnh.",404);
        await c.ExecuteAsync("UPDATE dbo.AssetImages SET IsPrimary=CASE WHEN Id=@imageId THEN 1 ELSE 0 END WHERE AssetId=@id",new {id,imageId},tx);
        await tx.CommitAsync();return Ok(new {message="Đã đặt ảnh đại diện."});
    }
    [HttpDelete("{imageId:int}"),Authorize(Roles="admin")]
    public async Task<IActionResult> Delete(int id,int imageId)
    {
        await using var c=await db.Open();await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        await c.ExecuteScalarAsync<int?>("SELECT Id FROM dbo.Assets WITH(UPDLOCK,HOLDLOCK) WHERE Id=@id",new {id},tx);
        var wasPrimary=await c.QuerySingleOrDefaultAsync<bool?>("DELETE dbo.AssetImages OUTPUT DELETED.IsPrimary WHERE Id=@imageId AND AssetId=@id",new {id,imageId},tx);
        Rules.Require(wasPrimary.HasValue,"Không tìm thấy ảnh.",404);
        if(wasPrimary==true) await c.ExecuteAsync("UPDATE dbo.AssetImages SET IsPrimary=1 WHERE Id=(SELECT TOP 1 Id FROM dbo.AssetImages WHERE AssetId=@id ORDER BY Id)",new {id},tx);
        await tx.CommitAsync();return Ok(new {message="Đã xóa ảnh."});
    }
}

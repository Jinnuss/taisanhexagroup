using Dapper;
using Hexa.Api.Models;
namespace Hexa.Api.Services;
public class DetailService(Database db,IConfiguration config)
{
    public string ImageUrl(Guid publicId,int imageId)=>$"{config["PublicApiUrl"]!.TrimEnd('/')}/api/public/assets/{publicId}/images/{imageId}";
    public async Task<List<ImageRow>> Images(AssetRow a)
    {
        await using var c=await db.Open();
        var images=(await c.QueryAsync<ImageRow>("SELECT Id,FileName,MimeType,FileSize,IsPrimary,CreatedAt FROM dbo.AssetImages WHERE AssetId=@id ORDER BY IsPrimary DESC,Id",new {id=a.Id})).ToList();
        foreach(var image in images) image.Url=ImageUrl(a.PublicId,image.Id);return images;
    }
    public async Task<object> History(int id)
    {
        await using var c=await db.Open();
        var history=await c.QueryAsync("""
            SELECT h.Id,h.ActionType,h.Description,h.OldData,h.NewData,h.CreatedAt,u.FullName PerformedByName
            FROM dbo.AssetHistory h LEFT JOIN dbo.Users u ON u.Id=h.PerformedBy WHERE h.AssetId=@id ORDER BY h.Id DESC
            """,new {id});
        var assignments=await c.QueryAsync("""
            SELECT aa.Id,aa.AssignedToSnapshot,aa.AssignedDate,aa.ReturnedDate,aa.AssignNote,aa.ReturnNote,aa.IsActive,
            u.FullName AssignedByName,r.FullName ReturnedByName FROM dbo.AssetAssignments aa
            LEFT JOIN dbo.Users u ON u.Id=aa.AssignedBy LEFT JOIN dbo.Users r ON r.Id=aa.ReturnedBy
            WHERE aa.AssetId=@id ORDER BY aa.Id DESC
            """,new {id});
        return new {history,assignments};
    }
}

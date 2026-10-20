using Hexa.Api.Services;
using Hexa.Api.Models;
using Hexa.Api.Controllers;
using ClosedXML.Excel;
using System.Text.Json;

int count=0;
void Check(bool condition,string name) { if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++; }
Check(CurrentUser.MayRead(true,null,2),"Admin can read all companies");
Check(CurrentUser.MayRead(false,1,1),"Employee can read own company");
Check(!CurrentUser.MayRead(false,1,2),"Employee denied other company");
Check(!CurrentUser.MayRead(false,null,null),"Unassigned employee fails closed");
Check(!CurrentUser.MayRead(false,1,null),"Employee denied unassigned asset");
Check(Rules.Status("using",null)=="available","No assignee implies available");
Check(Rules.Status("available","A")=="using","Assignee implies in use");
Check(Rules.Status("broken","A")=="broken","Special status preserved");
bool rejected=false;try{Rules.Password("password");}catch(ApiException){rejected=true;}
Check(rejected,"Weak password rejected");
Rules.Password("Test-only-2026!");
var hash=BCrypt.Net.BCrypt.HashPassword("Test-only-2026!");
Check(BCrypt.Net.BCrypt.Verify("Test-only-2026!",hash.Replace("$2a$","$2b$")),"bcrypt 2b compatible");
Check(!ImagesController.ValidImage([60,115,99,114,105,112,116],"image/png"),"HTML cannot masquerade as PNG");
Check(!ImagesController.ValidImage([255,216,255],"image/svg+xml"),"SVG upload rejected");
var json=JsonSerializer.Serialize(new UserRow {PasswordHash="sensitive",TokenVersion=4});
Check(!json.Contains("PasswordHash")&&!json.Contains("TokenVersion"),"No password hashes in user JSON");
using var wb=ExcelController.Workbook([new AssetRow {AssetCode="=1+1",AssetName="Máy tính",PurchasePrice=123456,PurchaseDate=new DateTime(2026,9,12)}],"Tài sản");
using var ms=new MemoryStream();wb.SaveAs(ms);ms.Position=0;
using var read=new XLWorkbook(ms);var ws=read.Worksheet(1);
Check(ws.Cell(2,1).GetString()=="=1+1"&&!ws.Cell(2,1).HasFormula,"Excel formula-like input remains text");
Check(ws.Cell(1,11).GetString()=="Ghi chú"&&ws.Cell(2,3).GetString()=="Máy tính","Vietnamese Excel round-trip");
Check(ws.Cell(2,8).GetDateTime()==new DateTime(2026,9,12)&&ws.Cell(2,9).GetDouble()==123456,"Excel dates and prices round-trip");
Console.WriteLine($"{count} checks passed. SQL integration is a separate test.");

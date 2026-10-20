using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Dapper;
using Hexa.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 35 * 1024 * 1024);
builder.Services.AddControllers().AddJsonOptions(o => {
    // V8 React expects PascalCase SQL rows and lower-case anonymous response envelopes.
    o.JsonSerializerOptions.PropertyNamingPolicy = null;
    o.JsonSerializerOptions.DictionaryKeyPolicy = null;
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    o.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
});
builder.Services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ctx =>
    new BadRequestObjectResult(new { message = "Vui lòng kiểm tra dữ liệu.", errors = ctx.ModelState
        .Where(x=>x.Value?.Errors.Count>0).Select(x=>new {field=x.Key, errors=x.Value!.Errors.Select(e=>e.ErrorMessage)}) }));
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Database>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<AssetService>();
builder.Services.AddScoped<DetailService>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p=>p.WithOrigins(
    builder.Configuration.GetSection("ClientOrigins").Get<string[]>() ?? ["http://localhost:5173"])
    .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o=>{
    o.RejectionStatusCode=429;
    o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "local", _=>new FixedWindowRateLimiterOptions {
            PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0 }));
});
var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
if (Encoding.UTF8.GetByteCount(jwtKey) < 32) throw new InvalidOperationException("Cấu hình Jwt:Key tối thiểu 32 byte bằng user-secrets hoặc biến môi trường.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o=>{
    o.MapInboundClaims=false;
    o.TokenValidationParameters=new TokenValidationParameters {
        ValidateIssuer=true, ValidIssuer=builder.Configuration["Jwt:Issuer"],
        ValidateAudience=true, ValidAudience=builder.Configuration["Jwt:Audience"],
        ValidateLifetime=true, ValidateIssuerSigningKey=true, RequireExpirationTime=true,
        IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidAlgorithms=[SecurityAlgorithms.HmacSha256], NameClaimType="sub", RoleClaimType="role",
        ClockSkew=TimeSpan.FromSeconds(30)
    };
    o.Events=new JwtBearerEvents {
        OnTokenValidated=async ctx=>{
            if(!int.TryParse(ctx.Principal?.FindFirstValue("sub"),out var id) ||
                !int.TryParse(ctx.Principal?.FindFirstValue("ver"),out var version)) { ctx.Fail("Invalid token"); return; }
            var user=await ctx.HttpContext.RequestServices.GetRequiredService<Database>().UserById(id);
            if(user is null || !user.IsActive || user.TokenVersion!=version) { ctx.Fail("Session expired"); return; }
            // Authorization uses current database role/company, not stale token privileges.
            ctx.Principal=new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub",user.Id.ToString()),new Claim("role",user.Role),
                new Claim("departmentId",user.DepartmentId?.ToString() ?? "")
            ],"Bearer","sub","role"));
        }
    };
});
builder.Services.AddAuthorization(o=>o.FallbackPolicy=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
var app=builder.Build();
app.Use(async (ctx,next)=>{
    try { await next(); }
    catch(Exception ex) {
        var status=ex is ApiException api ? api.Status : ex is SqlException sql && sql.Number is 2601 or 2627 or 547 or 1205 ? 409 : 500;
        var message=ex is ApiException known ? known.Message : status==409 ? "Dữ liệu bị trùng, có liên kết hoặc vừa thay đổi. Hãy tải lại và thử lại." : "Không thể xử lý yêu cầu. Kiểm tra kết nối SQL Server và log máy chủ.";
        if(status==500) app.Logger.LogError(ex,"Request failed {Path}",ctx.Request.Path);
        if(!ctx.Response.HasStarted) { ctx.Response.Clear();ctx.Response.StatusCode=status;await ctx.Response.WriteAsJsonAsync(new {message}); }
    }
});
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
if(app.Environment.IsDevelopment()) {
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(o=>o.SwaggerEndpoint("/openapi/v1.json","Hexa .NET API"));
}
app.UseAuthorization();
app.MapGet("/",()=>new {message="QuanLyTaiSanHexa ASP.NET Core API"}).AllowAnonymous();
app.MapGet("/api/health",async(Database db)=>{
    await using var c=await db.Open();await c.ExecuteScalarAsync<int>("SELECT 1");
    return Results.Ok(new {success=true,message="API đã kết nối SQL Server."});
}).AllowAnonymous();
app.MapControllers();

if(args.Contains("--create-admin")) {
    using var scope=app.Services.CreateScope();
    var db=scope.ServiceProvider.GetRequiredService<Database>();
    var email=builder.Configuration["Bootstrap:Email"]?.Trim().ToLowerInvariant();
    var password=builder.Configuration["Bootstrap:Password"];
    Rules.Require(!string.IsNullOrWhiteSpace(email),"Thiếu Bootstrap:Email.");Rules.Password(password);
    await using var c=await db.Open();
    await using var tx=(SqlTransaction)await c.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    Rules.Require(!await c.ExecuteScalarAsync<bool>("SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.Users WHERE Email=@email) THEN 1 ELSE 0 END",new {email},tx),"Email đã tồn tại. Không ghi đè tài khoản.");
    await c.ExecuteAsync("INSERT dbo.Users(FullName,Email,PasswordHash,Role,IsActive) VALUES(@name,@email,@hash,'admin',1)",
        new {name=builder.Configuration["Bootstrap:Name"] ?? "Quản trị Hexa",email,hash=BCrypt.Net.BCrypt.HashPassword(password!,12)},tx);
    await tx.CommitAsync();Console.WriteLine("Đã tạo quản trị viên.");return;
}
app.Run();

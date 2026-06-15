using System.Text;
using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ============ 配置 ============
builder.Services.Configure<KingdeeSettings>(builder.Configuration.GetSection("KingdeeSettings"));
builder.Services.Configure<WeComSettings>(builder.Configuration.GetSection("WeComSettings"));

// ============ 服务容器 ============
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "金蝶云星空物料查询 API",
        Version = "v1",
        Description = "用于移动端的物料信息查询；支持企业微信 OAuth2 登录。"
    });
});

// CORS：允许所有来源，便于联调
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ============ JWT 鉴权 ============
var weComSection = builder.Configuration.GetSection("WeComSettings");
var jwtSecret = weComSection["JwtSecret"] ?? "";
if (string.IsNullOrWhiteSpace(jwtSecret)) jwtSecret = "please-change-this-to-a-long-secret-key";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "Kingdee.MaterialAPI",
            ValidAudience = "WeComUser",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
        // 前端也通过 query 或 cookie 传 token 时兼容
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["token"].FirstOrDefault()
                            ?? context.Request.Cookies["wct"];
                if (!string.IsNullOrWhiteSpace(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ============ 业务服务 ============
builder.Services.AddScoped<KingdeeApiClient>();
builder.Services.AddScoped<WeComAuthService>();
builder.Services.AddScoped<IMaterialService, MaterialService>();

// ============ 构建 ============
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "金蝶物料查询API v1");
    });
}

// 前端静态文件（frontend 目录）
var frontendPath = Path.Combine(
    Directory.GetCurrentDirectory().EndsWith(Path.Combine("backend", "Kingdee.MaterialAPI"))
        ? Path.Combine("..", "..", "frontend")
        : "frontend");
var fullFrontendPath = Path.GetFullPath(frontendPath);
if (Directory.Exists(fullFrontendPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(fullFrontendPath),
        RequestPath = ""
    });
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(fullFrontendPath),
        DefaultFileNames = new[] { "index.html" }
    });
}

app.UseCors("AllowAll");

// 身份认证
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ============ 最小 API ============
app.MapGet("/api/health", (IOptions<KingdeeSettings> settings) => new
{
    Status = "OK",
    Timestamp = DateTime.Now,
    Mode = settings.Value.Enable ? "Kingdee" : "Mock",
    ServerUrl = settings.Value.ServerUrl
});

app.MapGet("/api/config", (IOptions<KingdeeSettings> kingdee, IOptions<WeComSettings> wecom) => new
{
    Mode = kingdee.Value.Enable ? "Kingdee" : "Mock",
    HasKingdeeUrl = !string.IsNullOrWhiteSpace(kingdee.Value.ServerUrl),
    WeComConfigured =
        !string.IsNullOrWhiteSpace(wecom.Value.CorpId) &&
        !string.IsNullOrWhiteSpace(wecom.Value.Secret),
    CorpId = wecom.Value.CorpId
});

// SPA 兜底：任何 GET 非 API 请求找不到都返回 index.html（兼容深链）
app.MapFallback(context =>
{
    if (context.Request.Method != HttpMethods.Get) return Task.CompletedTask;
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api")) return Task.CompletedTask;
    var indexFile = Path.Combine(fullFrontendPath, "index.html");
    if (!System.IO.File.Exists(indexFile)) return Task.CompletedTask;
    context.Response.ContentType = "text/html; charset=utf-8";
    return context.Response.SendFileAsync(indexFile);
});

app.Run();

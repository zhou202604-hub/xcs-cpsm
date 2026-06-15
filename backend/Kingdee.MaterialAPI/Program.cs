using System.Text;
using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ========= 配置 =========
builder.Services.Configure<KingdeeSettings>(builder.Configuration.GetSection("KingdeeSettings"));
builder.Services.Configure<WeComSettings>(builder.Configuration.GetSection("WeComSettings"));

// ========= 单例/作用域服务 =========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "金蝶物料查询 + 企业微信 / PC 管理后台 API",
        Version = "v1"
    });
});

// CORS（联调时浏览器需要）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ========= JWT（管理员登录用，企业微信登录另外签发） =========
var jwtSecret = builder.Configuration["WeComSettings:JwtSecret"] ?? "please-change-this-to-a-long-random-secret-key-please-change";
builder.Services.AddAuthentication()
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
    })
    .AddJwtBearer("AdminJwt", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "Kingdee.MaterialAPI",
            ValidAudience = "AdminUser",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(30)
        };
    });
builder.Services.AddAuthorization();

// 业务服务
builder.Services.AddSingleton<AppConfigStore>();         // 配置持久化
builder.Services.AddScoped<KingdeeApiClient>();           // 金蝶 Web API 客户端
builder.Services.AddScoped<KingdeeMaterialMapper>();      // 字段映射器（热更新）
builder.Services.AddScoped<WeComAuthService>();           // 企业微信 OAuth / JS-SDK 签名
builder.Services.AddScoped<IMaterialService, MaterialService>(); // 物料数据服务

// ========= 构建 =========
var app = builder.Build();

// 开发环境启 Swagger（PC 管理后台也可以靠这个调试 API）
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "金蝶物料查询 API");
    });
}

// 前端页面（移动端）：托管 /frontend
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

// PC 管理后台：托管 /admin 目录，访问地址 /admin/index.html
var adminPath = Path.Combine(
    Directory.GetCurrentDirectory().EndsWith(Path.Combine("backend", "Kingdee.MaterialAPI"))
        ? Path.Combine("..", "..", "admin")
        : "admin");
var fullAdminPath = Path.GetFullPath(adminPath);
if (Directory.Exists(fullAdminPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(fullAdminPath),
        RequestPath = "/admin"
    });
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ========= 轻量 API =========
app.MapGet("/api/health", (AppConfigStore cfgStore) =>
{
    var cfg = cfgStore.Get();
    return new
    {
        Status = "OK",
        Mode = cfg.KingdeeEnable ? "Kingdee" : "Mock",
        WeCom = cfg.WeComEnable ? "Enabled" : "Disabled",
        Time = DateTime.Now
    };
});

app.MapGet("/api/config", (AppConfigStore cfgStore) =>
{
    var cfg = cfgStore.Get();
    return new
    {
        Mode = cfg.KingdeeEnable ? "Kingdee" : "Mock",
        FormId = cfg.KingdeeMaterialFormId,
        FieldMappings = cfg.FieldMappings.Where(f => f.Enabled).ToList(),
        WeComConfigured = cfg.WeComEnable && !string.IsNullOrWhiteSpace(cfg.WeComCorpId)
    };
});

// 深链路由兜底（企业微信里直接 https://域名/ 访问）
app.MapFallback(context =>
{
    if (context.Request.Method != HttpMethods.Get) return Task.CompletedTask;
    var path = context.Request.Path.ToString();
    var file = path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
        ? Path.Combine(fullAdminPath, "index.html")
        : Path.Combine(fullFrontendPath, "index.html");
    if (!File.Exists(file)) return Task.CompletedTask;
    context.Response.ContentType = "text/html; charset=utf-8";
    return context.Response.SendFileAsync(file);
});

app.Run();

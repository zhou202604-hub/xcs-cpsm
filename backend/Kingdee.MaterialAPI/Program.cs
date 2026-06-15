using Kingdee.MaterialAPI.Models;
using Kingdee.MaterialAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// === 配置 ===
builder.Services.Configure<KingdeeSettings>(
    builder.Configuration.GetSection("KingdeeSettings"));

// === 服务容器 ===
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "金蝶云星空物料查询 API",
        Version = "v1",
        Description = "用于移动端的物料信息查询，可切换为金蝶云星空 Web API 模式。"
    });
});

// CORS：允许所有来源，便于前端联调
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 业务服务
builder.Services.AddScoped<KingdeeApiClient>();
builder.Services.AddScoped<IMaterialService, MaterialService>();

// === 构建 ===
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "金蝶物料查询API v1");
    });
}

// 提供前端静态文件（把 frontend 目录挂到根路径，方便直接预览）
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
app.UseAuthorization();
app.MapControllers();

// 健康检查
app.MapGet("/api/health", (IOptions<KingdeeSettings> settings) => new
{
    Status = "OK",
    Timestamp = DateTime.Now,
    Mode = settings.Value.Enable ? "Kingdee" : "Mock",
    ServerUrl = settings.Value.ServerUrl
});

// 读取配置接口（前端可据此判断模式）
app.MapGet("/api/config", (IOptions<KingdeeSettings> settings) => new
{
    Mode = settings.Value.Enable ? "Kingdee" : "Mock",
    HasKingdeeUrl = !string.IsNullOrWhiteSpace(settings.Value.ServerUrl)
});

app.Run();

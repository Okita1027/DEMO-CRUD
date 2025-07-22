using System.Reflection;
using System.Text;
using DEMO_CRUD.Data;
using DEMO_CRUD.Exceptions;
using DEMO_CRUD.Services;
using DEMO_CRUD.Services.Impl;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ������־
// var logPath = builder.Configuration["Logging:LogPath"] ?? "Logs/";
// var logFilePath = Path.Combine(logPath, "log-.txt");
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Destructure.ByTransforming<DateTime>(datetime => datetime.ToLocalTime())
    // .MinimumLevel.Information()
    // .WriteTo.File(
    //     new RenderedCompactJsonFormatter(), // ��־��ʽ��ΪJSON
    //     logFilePath,
    //     rollingInterval: RollingInterval.Day,
    //     retainedFileCountLimit: 7)  // �������7�����־
    .CreateLogger();


// Add services to the container.
builder.Services.AddScoped<IBooksService, BooksServiceImpl>();
builder.Services.AddScoped<IUsersService, UsersServiceImpl>();
builder.Services.AddScoped<ILoansService, LoansServiceImpl>();
// JWT���ÿ�ʼ
IConfigurationSection jwtSettings = builder.Configuration.GetSection("JwtSettings");
string secretKey = jwtSettings["SecretKey"]; // JWTǩ����Կ
string issuer = jwtSettings["Issuer"]; // JWT���Ʒ�����
string audience = jwtSettings["Audience"]; // JWT���ƽ�����
if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT ��Կû�����ú�");
}

byte[] key = Encoding.ASCII.GetBytes(secretKey); // ����Կ�ַ���תΪ�ֽ�����
builder.Services.AddAuthentication(options =>
{
    // ����Ĭ�ϵ���֤��������ս����ΪJWT Bearer
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    // �Ƿ�����ǿ��HTTPS
    options.RequireHttpsMetadata = false;
    // �Ƿ���HttpContext�д洢JWT
    options.SaveToken = true; 
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuerSigningKey = true, // ��֤ǩ����Կ��ȷ������û�б��۸�
        IssuerSigningKey = new SymmetricSecurityKey(key), // ����ǩ������Կ
        ValidateIssuer = true, // ��֤���Ƶķ�����
        ValidateAudience = true, // ��֤���ƵĽ�����
        ValidateLifetime = true, // ��֤������Ч�ڣ�����ʱ�䣩
        ValidIssuer = issuer, // ��Ч�ķ�����
        ValidAudience = audience, // ��Ч�Ľ�����
        ClockSkew = TimeSpan.Zero // ������Ч��ʱ��ƫ������Ĭ��Ϊ5���ӣ���������Ϊ0����ʾ������Ч�ڱ����뵱ǰʱ��һ��
    };
});
builder.Services.AddAuthentication(); // ������Ȩ����
// JWT���ý���

// �������ļ��л�ȡ�����ַ���
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// ע��DbContext����
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // AutoDetect ������ Pomelo �Զ����MySQL�汾����������趨
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// ����ѭ�����õ�һ�ּ򵥷�ʽ�������Ƽ���ô���������ǰ�����Դ����ص�����
//builder.Services.AddControllers()
//    .AddJsonOptions(options =>
//    {
//        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
//    });

// ע��Mapster���ķ���
builder.Services.AddMapster();
// ִ���Զ����Mapsterӳ������
MapsterConfig.Configure();
builder.Services.AddResponseCaching();
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new ArgumentExceptionFilter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ��������־�м��-Serilog
builder.Host.UseSerilog();


var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    // app.UseExceptionHandler("/exceptions");
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/exceptions");
}
// app.UseRouting();    // .NET 6֮����ʹ����MapXXX�����Զ�����UseRouting()

// ��֤�м����������Ȩ�м��֮ǰ
app.UseAuthentication(); // ������֤����������֤�����е� JWT
app.UseAuthorization(); // ������Ȩ�������û���ݺͲ��Ծ����Ƿ��������

app.UseResponseCaching();

// ��¼ÿ���������ϸ��Ϣ������·����״̬�롢��ʱ�ȡ�
app.UseSerilogRequestLogging();

app.MapControllers();

Log.Information("Application is starting...");

app.Run();

Log.Information("Application is shutting down...");
Log.CloseAndFlush();
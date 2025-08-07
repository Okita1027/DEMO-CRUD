using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using book_backend.Data;
using book_backend.Exceptions;
using book_backend.Services;
using book_backend.Services.Impl;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ������־
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Destructure.ByTransforming<DateTime>(datetime => datetime.ToLocalTime())
    .CreateLogger();


// ע���Զ������
builder.Services.AddScoped<IBooksService, BooksServiceImpl>();
builder.Services.AddScoped<IUsersService, UsersServiceImpl>();
builder.Services.AddScoped<ILoansService, LoansServiceImpl>();

// ���ÿ���
builder.Services.AddCors(options =>
{
    IConfigurationSection corsSettings = builder.Configuration.GetSection("Cors");
    string[] allowedOrigins = corsSettings.GetSection("AllowOrigins").Get<string[]>() ?? Array.Empty<string>();
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials(); // �����Ҫ����ƾ�ݣ��� cookies��
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
    });
});

// ��־��ǿ����
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .Destructure.ByTransforming<DateTime>(datetime => datetime.ToLocalTime()));


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
}, ServiceLifetime.Scoped);

// ����ѭ�����õ�һ�ּ򵥷�ʽ�������Ƽ���ô���������ǰ�����Դ����ص����ݡ��˷��������ڶ��ס�
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve;
    });

// ע��Mapster���ķ���
builder.Services.AddMapster();
// ִ���Զ����Mapsterӳ������
MapsterConfig.Configure();
builder.Services.AddResponseCaching();
builder.Services
    .AddControllers(options => { options.Filters.Add(new ArgumentExceptionFilter()); })
    .AddJsonOptions(options =>
    {
        // ����JSON���л�ʱʹ���շ�������������ĸСд��
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
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

// ʹ����ΪAllowAll�Ŀ������
app.UseCors("AllowAll");

// ��֤�м����������Ȩ�м��֮ǰ
app.UseAuthentication(); // ������֤����������֤�����е� JWT
app.UseAuthorization(); // ������Ȩ�������û���ݺͲ��Ծ����Ƿ��������

app.UseResponseCaching();

// ��¼ÿ���������ϸ��Ϣ������·����״̬�롢��ʱ�ȡ�
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        if (ex != null)
            return LogEventLevel.Error;
            
        if (httpContext.Response.StatusCode > 499)
            return LogEventLevel.Error;
            
        if (httpContext.Response.StatusCode > 399)
            return LogEventLevel.Warning;
            
        return LogEventLevel.Information;
    };
    
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].FirstOrDefault());
    };
});

app.MapControllers();

Log.Information("Application is starting...");

app.Run();

Log.Information("Application is shutting down...");
Log.CloseAndFlush();
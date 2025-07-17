using System.Text;
using DEMO_CRUD.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// ������־
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        "Logs/log-.txt",
        rollingInterval: RollingInterval.Day)
    .CreateLogger();


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// JWT���ÿ�ʼ
IConfigurationSection jwtSettings = builder.Configuration.GetSection("JwtSettings");
string secretKey = jwtSettings["SecretKey"];    // JWTǩ����Կ
string issuer = jwtSettings["Issuer"];  // JWT���Ʒ�����
string audience = jwtSettings["Audience"];  // JWT���ƽ�����
if (string.IsNullOrEmpty(secretKey))
{
    throw new InvalidOperationException("JWT ��Կû�����ú�");
}

byte[] key = Encoding.ASCII.GetBytes(secretKey);    // ����Կ�ַ���תΪ�ֽ�����
builder.Services.AddAuthentication(options =>
{
    // ����Ĭ�ϵ���֤��������ս����ΪJWT Bearer
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    // �ڿ���������������Ϊfalse,����������������Ϊtrue��ǿ��HTTPS
    options.RequireHttpsMetadata = false;
    options.SaveToken = true; // �Ƿ���HttpContext�д洢JWT
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuerSigningKey = true, // ��֤ǩ����Կ��ȷ������û�б��۸�
        IssuerSigningKey = new SymmetricSecurityKey(key),   // ����ǩ������Կ
        ValidateIssuer = true, // ��֤���Ƶķ�����
        ValidateAudience = true, // ��֤���ƵĽ�����
        ValidateLifetime = true, // ��֤������Ч�ڣ�����ʱ�䣩
        ValidIssuer = issuer, // ��Ч�ķ�����
        ValidAudience = audience, // ��Ч�Ľ�����
        ClockSkew = TimeSpan.Zero // ������Ч��ʱ��ƫ������Ĭ��Ϊ5���ӣ���������Ϊ0����ʾ������Ч�ڱ����뵱ǰʱ��һ��
    };
    
});
builder.Services.AddAuthentication();   // ������Ȩ����
// JWT���ý���

// �������ļ��л�ȡ�����ַ���
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// ע��DbContext����
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // ���� DbContext ʹ�� MySQL ���ݿ�
    // ʹ�� AutoDetect ������ Pomelo �Զ����MySQL�汾����������趨
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// ����ѭ��������һ�ּ򵥷�ʽ�������Ƽ���ô���������ǰ�����Դ����ص�����
//builder.Services.AddControllers()
//    .AddJsonOptions(options =>
//    {
//        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.Preserve; // ����ѭ������
//    });
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Host.UseSerilog();  // ��������־�м��-Serilog


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ��֤�м����������Ȩ�м��֮ǰ
app.UseAuthentication(); // ������֤����������֤�����е� JWT
app.UseAuthorization();  // ������Ȩ�������û���ݺͲ��Ծ����Ƿ��������

app.MapControllers();

app.Run();

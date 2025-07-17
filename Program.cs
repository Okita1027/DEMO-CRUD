using DEMO_CRUD.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json.Serialization;

// ������־
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        "Logs/log-.txt",
        rollingInterval: RollingInterval.Day)
    .CreateLogger();


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// �������ļ��л�ȡ�����ַ���
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// ע��DbContext����
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // ���� DbContext ʹ�� MySQL ���ݿ�
    // ʹ�� AutoDetect ������ Pomelo �Զ����MySQL�汾����������趨
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// ����ѭ��������һ�ּ򵥷�ʽ
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

app.UseAuthorization();

app.MapControllers();

app.Run();

using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Application.Features.PmRouting.Services;
using GcsPreCheck.Application.Features.RulesEngine;
using GcsPreCheck.Application.Features.Tickets.Services;
using GcsPreCheck.Infrastructure.NetworkSecurity;
using GcsPreCheck.Infrastructure.Ocr;
using GcsPreCheck.Infrastructure.Reports;
using GcsPreCheck.Infrastructure.Security;
using GcsPreCheck.Infrastructure.Storage;
using GcsPreCheck.Persistence.Contexts;
using GcsPreCheck.WebApi.Hubs;
using GcsPreCheck.WebApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

// CORS for local Angular development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=ASPL-LOKESH\\SQLEXPRESS2019;Database=GcsPreCheckDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=Optional;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

// Storage, Security & Network Services
builder.Services.AddSingleton<IAttachmentStorageService, AttachmentStorageService>();
builder.Services.AddSingleton<IAntivirusScanner, AntivirusScanService>();
builder.Services.AddSingleton<IOcrEngine, LocalOcrService>();
builder.Services.AddSingleton<ITicketParameterExtractor, TicketParameterExtractor>();
builder.Services.AddScoped<IStudioNetworkValidator, StudioNetworkValidator>();
builder.Services.AddSingleton<IEphemeralCredentialVault, EphemeralCredentialVault>();

// Windows Ephemeral Credential IPC Server
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<INamedPipeServerService, NamedPipeSecurityModule>();
    builder.Services.AddHostedService<IpcCredentialServerBackgroundService>();
}

// Application Services
builder.Services.AddScoped<ITicketIngestionService, TicketIngestionService>();
builder.Services.AddScoped<IPmRoutingService, PmRoutingService>();
builder.Services.AddScoped<IFieldVerificationService, FieldVerificationService>();
builder.Services.AddScoped<ISopRuleEvaluationEngine, SopRuleEvaluationEngine>();
builder.Services.AddSingleton<IPdfReportGenerator, StandardPreCheckPdfReportGenerator>();

var app = builder.Build();

app.UseCors();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();
app.MapHub<PreCheckHub>("/hubs/precheck");

app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.Run();

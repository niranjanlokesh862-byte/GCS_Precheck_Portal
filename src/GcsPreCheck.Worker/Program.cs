using GcsPreCheck.Application.Common.Interfaces;
using GcsPreCheck.Application.Features.RulesEngine;
using GcsPreCheck.Infrastructure.NetworkSecurity;
using GcsPreCheck.Infrastructure.Security;
using GcsPreCheck.Persistence.Contexts;
using GcsPreCheck.Worker.Automation;
using GcsPreCheck.Worker.Safeguards;
using GcsPreCheck.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "GcsPreCheckWorkerService";
});

// Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=ASPL-LOKESH\\SQLEXPRESS2019;Database=GcsPreCheckDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=Optional;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

// Network Security & Forward Proxy
builder.Services.AddScoped<IStudioNetworkValidator, StudioNetworkValidator>();
builder.Services.AddSingleton<ILocalStudioForwardProxy, LocalStudioForwardProxy>();

// IPC Client for Ephemeral Credential Handoff
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<INamedPipeClientService, NamedPipeSecurityModule>();
}

// Rules Engine & Safeguards & Automation Engine
builder.Services.AddScoped<ISopRuleEvaluationEngine, SopRuleEvaluationEngine>();
builder.Services.AddSingleton<IReadOnlyRouteInterceptor, ReadOnlyRouteInterceptor>();
builder.Services.AddScoped<IStudioAutomationEngine, StudioAutomationEngine>();

// Hosted Background Worker
builder.Services.AddHostedService<PreCheckWorkerService>();

var host = builder.Build();
host.Run();

using System.Text.Json.Serialization;
using FluentValidation;
using Serilog;
using ServerGuard.Api.Configuration;
using ServerGuard.Api.Data;
using ServerGuard.Api.Detection;
using ServerGuard.Api.ErrorHandling;
using ServerGuard.Api.Notifications;
using ServerGuard.Api.Realtime;
using ServerGuard.Api.Reputation;
using ServerGuard.Api.Validation;
using ServerGuard.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

builder.Services.AddOpenApi();
builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddValidatorsFromAssemblyContaining<ServerMetricDtoValidator>();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddIpReputation(builder.Configuration);
builder.Services.AddAlertNotifications(builder.Configuration);
builder.Services.AddDetection(builder.Configuration);
builder.Services.AddRealtime();
builder.Services.AddWebClientCors(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(WebClientCors.PolicyName);

app.MapControllers();
app.MapRealtimeHubs();
app.MapHealthChecks(ApiRoutes.Health);

app.Run();

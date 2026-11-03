using PersonalAssistant.Api.Endpoints;
using PersonalAssistant.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersonalAssistant(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => "Personal Assistant is running");
app.MapWhatsAppWebhookEndpoints();

app.Run();

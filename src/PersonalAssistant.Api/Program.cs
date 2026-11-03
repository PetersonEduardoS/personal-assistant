using Microsoft.Extensions.Options;
using PersonalAssistant.Api.Endpoints;
using PersonalAssistant.Core.WhatsApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection(WhatsAppOptions.SectionName));

builder.Services.AddSingleton(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
    return new WebhookVerifier(options.VerifyToken);
});

var app = builder.Build();

app.MapGet("/", () => "Personal Assistant is running");
app.MapWhatsAppWebhookEndpoints();

app.Run();

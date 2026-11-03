using Microsoft.Extensions.Options;
using PersonalAssistant.Api.Workers;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;
using PersonalAssistant.Infrastructure.Claude;
using PersonalAssistant.Infrastructure.WhatsApp;

namespace PersonalAssistant.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersonalAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.Configure<ClaudeOptions>(configuration.GetSection(ClaudeOptions.SectionName));
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<WhatsAppOptions>>().Value);
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<ClaudeOptions>>().Value);

        // Receiving messages
        services.AddSingleton(serviceProvider =>
            new WebhookVerifier(serviceProvider.GetRequiredService<WhatsAppOptions>().VerifyToken));
        services.AddSingleton(serviceProvider =>
            new WebhookSignatureValidator(serviceProvider.GetRequiredService<WhatsAppOptions>().AppSecret));
        services.AddSingleton(serviceProvider =>
            new AllowedSenderPolicy(serviceProvider.GetRequiredService<WhatsAppOptions>().AllowedSenders));
        services.AddSingleton<WebhookPayloadParser>();
        services.AddSingleton<IIncomingMessageQueue, InMemoryIncomingMessageQueue>();

        // Processing and replying
        services.AddSingleton(new ProcessedMessageTracker());
        services.AddTransient<IncomingMessageProcessor>();
        services.AddHttpClient<IAssistantClient, ClaudeClient>();
        services.AddHttpClient<IMessageSender, WhatsAppClient>();
        services.AddHostedService<IncomingMessageWorker>();

        return services;
    }
}

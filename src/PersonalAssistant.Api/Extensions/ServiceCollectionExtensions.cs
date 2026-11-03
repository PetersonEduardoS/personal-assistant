using Microsoft.Extensions.Options;
using PersonalAssistant.Api.Workers;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.WhatsApp;

namespace PersonalAssistant.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWhatsAppWebhook(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));

        services.AddSingleton(serviceProvider => new WebhookVerifier(GetOptions(serviceProvider).VerifyToken));
        services.AddSingleton(serviceProvider => new WebhookSignatureValidator(GetOptions(serviceProvider).AppSecret));
        services.AddSingleton(serviceProvider => new AllowedSenderPolicy(GetOptions(serviceProvider).AllowedSenders));
        services.AddSingleton<WebhookPayloadParser>();
        services.AddSingleton<IIncomingMessageQueue, InMemoryIncomingMessageQueue>();
        services.AddHostedService<IncomingMessageWorker>();

        return services;
    }

    private static WhatsAppOptions GetOptions(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
}

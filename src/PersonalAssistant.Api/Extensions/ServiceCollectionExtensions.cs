using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalAssistant.Api.Database;
using PersonalAssistant.Api.Workers;
using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Deadlines;
using PersonalAssistant.Core.Emails;
using PersonalAssistant.Core.Media;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Notes;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.WhatsApp;
using PersonalAssistant.Core.WorkSchedules;
using PersonalAssistant.Infrastructure.Calendars;
using PersonalAssistant.Infrastructure.Claude;
using PersonalAssistant.Infrastructure.Deadlines;
using PersonalAssistant.Infrastructure.Emails;
using PersonalAssistant.Infrastructure.Persistence;
using PersonalAssistant.Infrastructure.Users;
using PersonalAssistant.Infrastructure.WhatsApp;
using PersonalAssistant.Infrastructure.WorkSchedules;

namespace PersonalAssistant.Api.Extensions;

public static class ServiceCollectionExtensions
{
    private const string DefaultDatabasePath = "Data/personal-assistant.db";
    private const string UsersTimeZoneId = "Europe/Dublin";

    public static IServiceCollection AddPersonalAssistant(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<WhatsAppOptions>(configuration.GetSection(WhatsAppOptions.SectionName));
        services.Configure<ClaudeOptions>(configuration.GetSection(ClaudeOptions.SectionName));
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<WhatsAppOptions>>().Value);
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<ClaudeOptions>>().Value);

        // Storage (the migrator runs before the worker starts)
        services.AddDbContext<AssistantDbContext>(options =>
            options.UseSqlite(BuildConnectionString(configuration, environment)));
        services.AddHostedService<DatabaseMigrator>();
        services.AddScoped<IConversationStore, EfConversationStore>();
        services.AddScoped<IMorningBriefStore, EfMorningBriefStore>();
        services.AddScoped<IAppointmentStore, EfAppointmentStore>();
        services.AddScoped<INoteStore, EfNoteStore>();
        services.AddScoped<IMorningBriefPreferenceStore, EfMorningBriefPreferenceStore>();
        services.AddTransient<AssistantToolFactory>();
        services.AddSingleton<IEmailInbox, ImapEmailInbox>();
        services.AddSingleton(TimeProvider.System);

        // Receiving messages
        services.AddSingleton(serviceProvider =>
            new WebhookVerifier(serviceProvider.GetRequiredService<WhatsAppOptions>().VerifyToken));
        services.AddSingleton(serviceProvider =>
            new WebhookSignatureValidator(serviceProvider.GetRequiredService<WhatsAppOptions>().AppSecret));
        services.AddSingleton(serviceProvider => new UserDirectory(
            serviceProvider.GetRequiredService<IConfiguration>()
                .GetSection(UserDirectory.SectionName)
                .Get<UserProfile[]>() ?? []));
        // The context file plus the notes saved from the chat (scoped, because the notes come from the database).
        services.AddSingleton<FileUserContextProvider>();
        services.AddScoped<IUserContextProvider>(serviceProvider => new NotesUserContextProvider(
            serviceProvider.GetRequiredService<FileUserContextProvider>(),
            serviceProvider.GetRequiredService<INoteStore>()));
        services.AddSingleton<IMorningBriefRoutineProvider, FileMorningBriefRoutineProvider>();
        services.AddSingleton<WebhookPayloadParser>();
        services.AddSingleton<IIncomingMessageQueue, InMemoryIncomingMessageQueue>();

        // Processing and replying
        services.AddTransient<IncomingMessageProcessor>();
        services.AddTransient<MorningBriefWriter>();
        services.AddTransient<MorningBriefScheduler>();
        services.AddTransient<AppointmentReminderService>();
        services.AddSingleton<MorningBriefDeliveryGate>();
        services.AddTransient<MorningBriefDelivery>();

        // Daily brief: the users' time zone and their work shifts
        services.AddSingleton(TimeZoneInfo.FindSystemTimeZoneById(UsersTimeZoneId));
        services.AddSingleton<CalendarFeedCache>();
        services.AddHttpClient<CalendarFeedClient>(client =>
            client.Timeout = TimeSpan.FromSeconds(15))
            .RemoveAllLoggers(); // Calendar URLs are bearer secrets, including their query strings.
        services.AddTransient<CachedCalendarFeed>();
        services.AddTransient<IWorkScheduleProvider, CalendarFeedWorkScheduleProvider>();
        services.AddTransient<IDeadlineProvider, CalendarFeedDeadlineProvider>();
        services.AddHttpClient<IAssistantClient, ClaudeClient>();
        services.AddHttpClient<IMessageSender, WhatsAppClient>();
        services.AddHttpClient<IMediaDownloader, WhatsAppMediaClient>(client =>
            client.Timeout = TimeSpan.FromSeconds(30))
            .RemoveAllLoggers(); // The temporary media URL works like a password and must not be logged.
        services.AddHostedService<IncomingMessageWorker>();
        services.AddHostedService<MorningBriefWorker>();
        services.AddHostedService<AppointmentReminderWorker>();

        return services;
    }

    private static string BuildConnectionString(IConfiguration configuration, IHostEnvironment environment)
    {
        var databasePath = configuration["Database:Path"] ?? DefaultDatabasePath;

        if (!Path.IsPathRooted(databasePath))
        {
            databasePath = Path.Combine(environment.ContentRootPath, databasePath);
        }

        return new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }
}

using PersonalAssistant.Core.WorkSchedules;
using PersonalAssistant.Core.Appointments;
using PersonalAssistant.Core.Deadlines;
using PersonalAssistant.Core.Emails;
using PersonalAssistant.Core.Media;
using PersonalAssistant.Core.Assistant;
using PersonalAssistant.Core.Conversations;
using PersonalAssistant.Core.Messaging;
using PersonalAssistant.Core.Users;
using PersonalAssistant.Core.MorningBriefs;
using PersonalAssistant.Core.Notes;
using PersonalAssistant.Core.Shopping;

namespace PersonalAssistant.Core.Tests.Messaging;

internal sealed class FakeMorningBriefStore : IMorningBriefStore
{
    public Dictionary<(string Phone, DateOnly Date), MorningBrief> Briefs { get; } = [];
    public Task<MorningBrief?> GetAsync(string phoneNumber, DateOnly date, CancellationToken cancellationToken = default) =>
        Task.FromResult(Briefs.GetValueOrDefault((phoneNumber, date)));
    public Task<bool> TryAddAsync(string phoneNumber, DateOnly date, string content, CancellationToken cancellationToken = default) =>
        Task.FromResult(Briefs.TryAdd((phoneNumber, date), new(phoneNumber, date, content, null)));
    public Task<bool> TryMarkDeliveredAsync(string phoneNumber, DateOnly date, CancellationToken cancellationToken = default)
    {
        if (!Briefs.TryGetValue((phoneNumber, date), out var brief) || brief.IsDelivered) return Task.FromResult(false);
        Briefs[(phoneNumber, date)] = brief with { DeliveredAt = DateTimeOffset.UtcNow };
        return Task.FromResult(true);
    }
}

internal sealed class FakeAssistantClient : IAssistantClient
{
    private readonly string? _reply;
    private readonly bool _fails;

    public FakeAssistantClient(string reply) => _reply = reply;

    private FakeAssistantClient() => _fails = true;

    public List<AssistantRequest> ReceivedRequests { get; } = [];

    public static FakeAssistantClient Failing() => new();

    public Task<string> GetReplyAsync(AssistantRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);

        return _fails
            ? Task.FromException<string>(new InvalidOperationException("Assistant failed"))
            : Task.FromResult(_reply!);
    }
}

internal sealed class FakeMessageSender : IMessageSender
{
    public Task SendTemplateAsync(string recipient, string templateName, string languageCode,
        IReadOnlyList<string>? bodyParameters = null,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public List<(string Recipient, string Text)> SentMessages { get; } = [];

    public Task SendTextAsync(string recipient, string text, CancellationToken cancellationToken = default)
    {
        SentMessages.Add((recipient, text));

        return Task.CompletedTask;
    }
}

/// <summary>
/// Keeps the conversation in a list, with the same duplicate rule as the real store.
/// </summary>
internal sealed class FakeConversationStore : IConversationStore
{
    private readonly HashSet<string> _messageIds = [];
    private readonly Dictionary<string, DateTimeOffset> _lastUserMessageAt = [];

    public List<(string PhoneNumber, ConversationTurn Turn)> Turns { get; } = [];

    public Task<bool> TryAddIncomingMessageAsync(
        string phoneNumber,
        string whatsAppMessageId,
        string content,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default)
    {
        if (!_messageIds.Add(whatsAppMessageId))
        {
            return Task.FromResult(false);
        }

        Turns.Add((phoneNumber, new ConversationTurn(ConversationRole.User, content)));

        if (!_lastUserMessageAt.TryGetValue(phoneNumber, out var lastSentAt) || receivedAt > lastSentAt)
        {
            _lastUserMessageAt[phoneNumber] = receivedAt;
        }

        return Task.FromResult(true);
    }

    public Task<DateTimeOffset?> GetLastUserMessageAtAsync(
        string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset? lastSentAt = _lastUserMessageAt.TryGetValue(phoneNumber, out var sentAt) ? sentAt : null;

        return Task.FromResult(lastSentAt);
    }

    public Task AddAssistantReplyAsync(string phoneNumber, string content, CancellationToken cancellationToken = default)
    {
        Turns.Add((phoneNumber, new ConversationTurn(ConversationRole.Assistant, content)));

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationTurn>> GetRecentTurnsAsync(
        string phoneNumber,
        int limit,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ConversationTurn> turns = Turns
            .Where(entry => entry.PhoneNumber == phoneNumber)
            .Select(entry => entry.Turn)
            .TakeLast(limit)
            .ToList();

        return Task.FromResult(turns);
    }
}

internal sealed class FakeUserContextProvider(string? context = null) : IUserContextProvider
{
    public Task<string?> GetContextAsync(UserProfile user, CancellationToken cancellationToken = default) =>
        Task.FromResult(context);
}

/// <summary>
/// A clock that always returns the same time, so tests do not depend on the real time.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeDeadlineProvider(UpcomingDeadlines? deadlines = null) : IDeadlineProvider
{
    public List<DateOnly> RequestedDates { get; } = [];

    public Task<UpcomingDeadlines> GetUpcomingAsync(
        UserProfile user,
        DateOnly fromDate,
        CancellationToken cancellationToken = default)
    {
        RequestedDates.Add(fromDate);

        return Task.FromResult(deadlines ?? UpcomingDeadlines.NotConfigured);
    }
}

/// <summary>
/// Keeps appointments in a list, with the same rules as the real store.
/// </summary>
internal sealed class FakeAppointmentStore : IAppointmentStore
{
    private long _nextId = 1;
    private readonly Dictionary<long, DateTimeOffset> _createdAt = [];

    public List<(string PhoneNumber, Appointment Appointment)> Appointments { get; } = [];

    /// <summary>The creation time given to the next appointments added.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.MinValue;

    public HashSet<long> SentReminders { get; } = [];

    public Task<IReadOnlyList<PendingReminder>> ListPendingRemindersAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PendingReminder> result = Appointments
            .Where(entry => entry.Appointment.ReminderMinutesBefore is not null
                && !SentReminders.Contains(entry.Appointment.Id)
                && entry.Appointment.Date >= fromDate
                && entry.Appointment.Date <= toDate)
            .Select(entry => new PendingReminder(entry.PhoneNumber, entry.Appointment, _createdAt[entry.Appointment.Id]))
            .ToList();

        return Task.FromResult(result);
    }

    public Task<bool> TryMarkReminderSentAsync(long appointmentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Appointments.Any(entry => entry.Appointment.Id == appointmentId) && SentReminders.Add(appointmentId));

    public Task<Appointment> AddAsync(
        string phoneNumber,
        string title,
        DateOnly date,
        TimeOnly time,
        int? reminderMinutesBefore = Appointment.DefaultReminderMinutes,
        CancellationToken cancellationToken = default)
    {
        var cleanTitle = title.Trim();

        if (cleanTitle.Length == 0 || cleanTitle.Length > Appointment.MaxTitleLength)
        {
            throw new ArgumentException("Invalid title.", nameof(title));
        }

        var appointment = new Appointment(_nextId++, cleanTitle, date, time, reminderMinutesBefore);
        Appointments.Add((phoneNumber, appointment));
        _createdAt[appointment.Id] = CreatedAt;

        return Task.FromResult(appointment);
    }

    public Task<IReadOnlyList<Appointment>> ListAsync(
        string phoneNumber,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Appointment> result = Appointments
            .Where(entry => entry.PhoneNumber == phoneNumber
                && entry.Appointment.Date >= fromDate
                && entry.Appointment.Date <= toDate)
            .Select(entry => entry.Appointment)
            .OrderBy(appointment => appointment.Date)
            .ThenBy(appointment => appointment.Time)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<Appointment?> CancelAsync(string phoneNumber, long appointmentId, CancellationToken cancellationToken = default)
    {
        var index = Appointments.FindIndex(entry => entry.PhoneNumber == phoneNumber && entry.Appointment.Id == appointmentId);

        if (index < 0)
        {
            return Task.FromResult<Appointment?>(null);
        }

        var appointment = Appointments[index].Appointment;
        Appointments.RemoveAt(index);

        return Task.FromResult<Appointment?>(appointment);
    }
}

/// <summary>
/// Returns the schedule set for each day, or a day without shifts.
/// </summary>
internal sealed class FakeWorkScheduleProvider : IWorkScheduleProvider
{
    public Dictionary<DateOnly, WorkSchedule> Days { get; } = [];

    public WorkSchedule? Always { get; set; }

    public List<DateOnly> RequestedDates { get; } = [];

    public Task<WorkSchedule> GetScheduleAsync(UserProfile user, DateOnly date, CancellationToken cancellationToken = default)
    {
        RequestedDates.Add(date);

        return Task.FromResult(Always ?? (Days.TryGetValue(date, out var schedule) ? schedule : WorkSchedule.WithShifts([])));
    }
}

/// <summary>
/// Keeps notes in a list, with the same rules as the real store.
/// </summary>
internal sealed class FakeNoteStore : INoteStore
{
    private long _nextId = 1;

    public List<(string PhoneNumber, UserNote Note)> Notes { get; } = [];

    public Task<UserNote> AddAsync(string phoneNumber, string content, CancellationToken cancellationToken = default)
    {
        var clean = content.Trim();

        if (clean.Length == 0 || clean.Length > UserNote.MaxContentLength)
        {
            throw new ArgumentException("Invalid note.", nameof(content));
        }

        var note = new UserNote(_nextId++, clean, DateTimeOffset.MinValue);
        Notes.Add((phoneNumber, note));

        return Task.FromResult(note);
    }

    public Task<IReadOnlyList<UserNote>> ListAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UserNote> result = Notes
            .Where(entry => entry.PhoneNumber == phoneNumber)
            .Select(entry => entry.Note)
            .OrderBy(note => note.Id)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<UserNote?> DeleteAsync(string phoneNumber, long noteId, CancellationToken cancellationToken = default)
    {
        var index = Notes.FindIndex(entry => entry.PhoneNumber == phoneNumber && entry.Note.Id == noteId);

        if (index < 0)
        {
            return Task.FromResult<UserNote?>(null);
        }

        var note = Notes[index].Note;
        Notes.RemoveAt(index);

        return Task.FromResult<UserNote?>(note);
    }
}

/// <summary>
/// An inbox with fixed emails. A user has email when their profile has email settings.
/// </summary>
internal sealed class FakeEmailInbox : IEmailInbox
{
    public List<EmailSummary> Emails { get; } = [];

    public Dictionary<string, EmailMessage> Messages { get; } = [];

    public EmailQuery? LastQuery { get; private set; }

    public bool Fails { get; set; }

    public bool IsConfigured(UserProfile user) => user.Email is not null;

    public Task<IReadOnlyList<EmailSummary>> ListRecentAsync(
        UserProfile user,
        EmailQuery query,
        CancellationToken cancellationToken = default)
    {
        LastQuery = query;

        if (Fails)
        {
            throw new EmailUnavailableException("IMAP is down");
        }

        return Task.FromResult<IReadOnlyList<EmailSummary>>(Emails.Take(query.MaxCount).ToList());
    }

    public Task<EmailMessage?> ReadAsync(UserProfile user, string emailId, CancellationToken cancellationToken = default)
    {
        if (Fails)
        {
            throw new EmailUnavailableException("IMAP is down");
        }

        return Task.FromResult(Messages.GetValueOrDefault(emailId));
    }
}

internal sealed class FakeMorningBriefPreferenceStore : IMorningBriefPreferenceStore
{
    public Dictionary<string, MorningBriefPreference> Preferences { get; } = [];

    public Task<MorningBriefPreference?> GetAsync(string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Preferences.GetValueOrDefault(phoneNumber));

    public Task SetAsync(string phoneNumber, MorningBriefPreference preference, CancellationToken cancellationToken = default)
    {
        Preferences[phoneNumber] = preference;
        return Task.CompletedTask;
    }

    public Task ClearAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        Preferences.Remove(phoneNumber);
        return Task.CompletedTask;
    }
}

internal sealed class FakeMediaDownloader : IMediaDownloader
{
    public byte[] Content { get; set; } = [1, 2, 3];

    public Exception? Failure { get; set; }

    public List<(string MediaId, long MaxBytes)> Downloads { get; } = [];

    public Task<DownloadedMedia> DownloadAsync(string mediaId, long maxBytes, CancellationToken cancellationToken = default)
    {
        Downloads.Add((mediaId, maxBytes));

        return Failure is null
            ? Task.FromResult(new DownloadedMedia(Content, "application/octet-stream"))
            : Task.FromException<DownloadedMedia>(Failure);
    }
}

/// <summary>
/// Keeps shopping items in a list, with the same rules as the real store.
/// </summary>
internal sealed class FakeShoppingListStore : IShoppingListStore
{
    private long _nextId = 1;

    public List<(string Household, ShoppingItem Item, bool Bought)> Items { get; } = [];

    public int ListCalls { get; private set; }

    public Task<ShoppingAddResult> AddAsync(string household, string name, string? quantity, string section,
        string addedBy, CancellationToken cancellationToken = default)
    {
        var existing = Items.FirstOrDefault(entry => entry.Household == household && !entry.Bought
            && string.Equals(entry.Item.Name, name.Trim(), StringComparison.CurrentCultureIgnoreCase));

        if (existing.Item is not null)
        {
            return Task.FromResult(new ShoppingAddResult(existing.Item, false));
        }

        var item = new ShoppingItem(_nextId++, name.Trim(), quantity, section, addedBy, DateTimeOffset.MinValue);
        Items.Add((household, item, false));

        return Task.FromResult(new ShoppingAddResult(item, true));
    }

    public Task<IReadOnlyList<ShoppingItem>> ListPendingAsync(string household, CancellationToken cancellationToken = default)
    {
        ListCalls++;
        IReadOnlyList<ShoppingItem> pending = Items
            .Where(entry => entry.Household == household && !entry.Bought)
            .Select(entry => entry.Item)
            .ToList();

        return Task.FromResult(pending);
    }

    public Task<int> MarkBoughtAsync(string household, IReadOnlyCollection<long> itemIds, CancellationToken cancellationToken = default)
    {
        var marked = 0;

        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Household == household && !Items[i].Bought && itemIds.Contains(Items[i].Item.Id))
            {
                Items[i] = Items[i] with { Bought = true };
                marked++;
            }
        }

        return Task.FromResult(marked);
    }

    public Task<ShoppingItem?> RemoveAsync(string household, long itemId, CancellationToken cancellationToken = default)
    {
        var index = Items.FindIndex(entry => entry.Household == household && !entry.Bought && entry.Item.Id == itemId);

        if (index < 0)
        {
            return Task.FromResult<ShoppingItem?>(null);
        }

        var item = Items[index].Item;
        Items.RemoveAt(index);

        return Task.FromResult<ShoppingItem?>(item);
    }
}

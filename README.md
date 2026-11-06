# Personal Assistant

A personal assistant that lets two users chat with Claude through WhatsApp. It knows who is talking, remembers recent conversations, uses a short context file about each person and can search the web for current information.

Built with ASP.NET Core and hosted on Railway, so it answers 24 hours a day.

## Features

- Chat with Claude through the WhatsApp Cloud API
- Each phone number is mapped to a user, and unknown numbers are ignored
- The last 20 messages of each user are sent as conversation history
- A Markdown context file per user is added to the system prompt
- Web search for weather, news, prices and other current information
- Webhook signature validation with the Meta App Secret
- Duplicate webhooks are detected by a unique index in the database
- A friendly fallback reply when the AI service is unavailable

## How it works

```
WhatsApp -> Meta -> POST /webhooks/whatsapp
  -> validate signature -> parse message -> check user
  -> answer 200 right away -> background queue
  -> save message -> load history and context -> Claude API
  -> WhatsApp Cloud API -> WhatsApp
```

The webhook answers Meta immediately and the message is processed by a background worker, because Meta retries slow webhooks.

## Project structure

```
src/
  PersonalAssistant.Api             Webhook endpoints, background worker, DI and configuration
  PersonalAssistant.Core            Domain models, interfaces and rules (no external dependencies)
  PersonalAssistant.Infrastructure  Claude and WhatsApp HTTP clients, EF Core persistence, context files
tests/
  PersonalAssistant.Core.Tests            Unit tests (xUnit)
  PersonalAssistant.Infrastructure.Tests  HTTP clients with a fake handler, EF Core with in-memory SQLite
  PersonalAssistant.Api.Tests             Integration tests with WebApplicationFactory
```

## Tech stack

- .NET 10, ASP.NET Core minimal APIs
- Entity Framework Core with SQLite
- WhatsApp Cloud API (Meta)
- Claude API (Anthropic), with the web search tool
- xUnit, WebApplicationFactory
- Docker and Railway

## Running locally

Requirements: .NET 10 SDK and ngrok.

1. Store the secrets with .NET user secrets (they are never committed):

   ```
   dotnet user-secrets init --project src/PersonalAssistant.Api --id personal-assistant-api
   ```

   Then add these keys to the secrets file:

   | Key | Description |
   |---|---|
   | `WhatsApp:AccessToken` | Permanent System User token from Meta |
   | `WhatsApp:AppSecret` | App Secret, used to validate webhook signatures |
   | `WhatsApp:VerifyToken` | Any random string, used once when the webhook is registered |
   | `Anthropic:ApiKey` | Claude API key |
   | `Users:0:PhoneNumber` | Phone number in international format, digits only |
   | `Users:0:Name` | Name used by the assistant |
   | `Users:0:ContextFilePath` | Optional path to a Markdown file about the user |
   | `Users:0:ShiftsCalendarUrl` | Optional iCalendar link with the user's work shifts (Findmyshift). Secret |

2. Run the API and expose it with ngrok:

   ```
   dotnet run --project src/PersonalAssistant.Api --launch-profile http
   ngrok http 5001
   ```

3. Register `https://<your-ngrok-domain>/webhooks/whatsapp` as the callback URL in the Meta app and subscribe to the `messages` field.

## Tests

```
dotnet test
```

## Deploy

The app runs on [Railway](https://railway.com) from the `Dockerfile` in the repository root.

- **Build:** a multi-stage Docker build restores the packages, runs all the tests and publishes the API. If a test fails, nothing is deployed.
- **Automatic deploys:** every push to `main` builds and deploys a new version.
- **Storage:** a Railway volume is mounted at `/data`. It holds the SQLite database (`/data/personal-assistant.db`) and the user context files (`/data/contexts`). EF Core migrations are applied on startup.
- **Single instance:** the service runs with one replica, because of SQLite and the in-memory queue.

### Environment variables

Configuration keys use a double underscore instead of a colon.

| Variable | Value |
|---|---|
| `RAILWAY_RUN_UID` | `0`, so the app can write to the volume (Railway mounts volumes as root) |
| `PORT` | `8080` |
| `WhatsApp__AccessToken` | Permanent System User token |
| `WhatsApp__AppSecret` | App Secret |
| `WhatsApp__VerifyToken` | Verify token |
| `Anthropic__ApiKey` | Claude API key |
| `Users__0__PhoneNumber` | Phone number, digits only |
| `Users__0__Name` | Name |
| `Users__0__ContextFilePath` | `/data/contexts/<name>.md` |
| `Users__0__ShiftsCalendarUrl` | Optional iCalendar link with the user's work shifts |

Values must be pasted without quotes or extra spaces. A wrong value shows up as a `401` in the Railway logs.

### Uploading the context files

The context files are personal, so they are not in the repository. Upload them to the volume with the Railway CLI:

```
railway login
railway link
railway ssh keys add
railway volume files upload ./contexts /contexts
railway volume files list /contexts
```

The files are read on every message, so an updated file is used right away without a redeploy.

### Webhook

Register `https://<your-app>.up.railway.app/webhooks/whatsapp` as the callback URL in the Meta app. The health check path is `/`.

## Roadmap

- [x] Phase 1: Chat through WhatsApp
- [x] Phase 2: Memory, user context and web search
- [x] Hosting on Railway
- [ ] Phase 3: Email summaries with suggested replies saved as Gmail drafts
- [ ] Phase 4: Usage alerts

### Backlog

- [ ] Events and reminders through WhatsApp (after completing stored morning brief delivery)
  - Save events in the database from messages such as “Tenho dentista dia 18 às 9h, na clínica X”.
  - Confirm the date, Dublin time and location; ask for missing or ambiguous details.
  - Include saved events in the daily brief and use their preparation/travel time to adjust the brief's scheduled time.
  - Support listing, editing and cancelling saved events.
  - Send requested reminders at the agreed time, respecting WhatsApp's 24-hour window and approved template requirements.

## Daily morning briefs

Set `Users__0__MorningBriefTime` (for example `07:30`) and
`Users__1__MorningBriefTime` (for example `08:00`) in Railway to enable the
scheduler for each user. Omit the setting to leave that user's schedule disabled.
Times are interpreted in `Europe/Dublin`, including summer time; they do not use
the server's local timezone. The template `morning_brief_ready` in `pt_BR` must
be approved in the WhatsApp account before enabling automatic briefs.

The worker checks every minute and at startup. It catches up for up to one hour
after the scheduled time; a later restart waits until the next day. It reads work
shifts, generates the brief and saves it before sending. Within the 24-hour
customer service window it sends the text and adds it to conversation history.
Outside that window it sends the template and keeps the brief undelivered.
When the user replies, today's undelivered brief is sent and added to conversation history before normal chat resumes on the next message. Delivery is serialized within the single running replica; keep one replica while using the SQLite volume. A network timeout after Meta accepts a message can still cause a duplicate on a later retry.

A stored brief prevents another scheduled send on the same day, including after
restarts. This intentionally favors avoiding duplicates: if a send fails or the
process stops between saving and sending, the worker does not retry that stored
brief automatically. Inspect `Morning brief failed` and the structured Meta error
logs to investigate. `TextSent` means the API accepted the text, not a WhatsApp
read or delivery receipt. Generation failures before saving can retry on the next
minute, within the catch-up window. No database migration is needed for scheduling.

### Routine-based timing

As an alternative to a fixed time, set `Users__0__MorningBriefRoutineFilePath`
to a private JSON file on the volume, for example
`/data/contexts/eduardo.routine.local.json`. This takes precedence over
`MorningBriefTime`. Without either setting the user's scheduler is disabled.
The routine is reloaded each minute; a configured missing or malformed file logs
an error instead of silently switching to a different time.

```json
{
  "PreparationMinutes": 30,
  "WorkTravelMinutes": 25,
  "LatestTime": "09:00:00",
  "Appointments": [
    {
      "Name": "Weekly class",
      "DayOfWeek": "Thursday",
      "From": "2026-08-24",
      "Until": "2026-12-31",
      "Start": "08:30:00",
      "TravelMinutes": 25
    },
    {
      "Name": "Dated class",
      "Date": "2026-11-17",
      "Start": "10:00:00",
      "TravelMinutes": 75
    }
  ]
}
```

The earliest appointment or work shift determines the brief time, subtracting
preparation and travel (including the arrival margin), capped at `LatestTime`.
For example: 08:30 class - 25 minutes travel - 30 preparation = 07:35 brief;
10:00 dated class - 75 travel - 30 preparation = 08:15. Free days and afternoon-only
days use 09:00. Early shifts clamp at midnight instead of wrapping into the evening.
Weekly appointments require an explicit date range, so a finished semester does
not keep affecting the next year's schedule. Add holiday exceptions by adjusting
these ranges. Suggested gym sessions are not fixed appointments.

Travel times are planning estimates, not live route or bus predictions. The work
feed is cached for five minutes (one minute on failure). If unavailable, timing
uses known classes or the latest time; it cannot account for an unknown early shift.
Keep personal routine files outside Git (`*.local.json` is ignored) and upload them
with the private context files. The local Eduardo routine uses the ten LVA dates
from his context and a provisional CCT end date of 31 December 2026; adjust this
when the final term/holiday dates are known. Karina can continue using a fixed time.


### Protected manual brief test

Set a random `MorningBriefs__TestKey` with at least 32 characters to enable
`POST /admin/morning-briefs/test/0` (0 is the first registered user). Send the key
in `X-Morning-Brief-Key`. The route is disabled without a sufficiently long key;
it accepts only registered user indexes. Never put the key in a URL or commit it.
The test bypasses the scheduled time but still creates at most one brief for that
user/date: a successful test counts as today's brief and prevents a second
scheduled notification. Remove the key after testing to disable the endpoint.

Rollback: redeploy the previous successful image and remove the routine/test-key
variables to disable the new scheduling and test route. No new migration is
required by this release. Verify the health endpoint, production startup logs,
manual send result, and reply-triggered delivery before considering the release
verified.

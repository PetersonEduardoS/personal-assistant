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

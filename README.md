# Lightning Lab 3 - One Codebase, Two Environments

**Topic:** Dev vs Production — Environment Configuration
**Stack:** .NET 10 · Blazor Web App · EF Core · SQLite (dev) · PostgreSQL (prod) · Docker

---

## Prerequisites

| Tool | Check |
|------|-------|
| .NET 10 SDK | `dotnet --version` |
| Docker Desktop (running) | `docker info` |
| Git | `git --version` |

---

## Project Structure

```
LightningLab3/
├── src/LightningLab3/
│   ├── Components/Pages/Games.razor   ← the page you will be observing
│   ├── Data/AppDbContext.cs           ← EF Core context + seed data
│   ├── Models/Game.cs
│   ├── appsettings.json              ← base config (always loaded)
│   ├── appsettings.Development.json  ← dev overrides
│   ├── appsettings.Production.json   ← prod overrides (no secrets)
│   ├── Program.cs                    ← Bug is here (Step 2)
│   └── Dockerfile
├── docker-compose.yml
├── .env.example                      ← copy this to .env before Step 3
└── README.md
```

---

## Step 1 — Clone, Run, and Observe the Environment Banner

Clone the repo and run the app:

```bash
dotnet run --project src/LightningLab3
```

Navigate to **http://localhost:5101/games**.

You should see a **blue banner** at the top of the page showing:

```
ASPNETCORE_ENVIRONMENT   Development
Database provider        SQLite (local file — games.db)
```

### How does the app know it is in Development?

Open `src/LightningLab3/Properties/launchSettings.json`.

You will see:

```json
"environmentVariables": {
  "ASPNETCORE_ENVIRONMENT": "Development"
}
```

This file is only used when you run the app locally (`dotnet run` or the IDE Run button).
It is **never deployed** — which is how the app gets a different environment in Docker.

### The config loading chain

.NET merges config in this order (later entries override earlier ones):

```
appsettings.json                     ← base defaults, always loaded
  └── appsettings.{Environment}.json ← overrides for this specific environment
        └── Environment variables    ← highest priority, overrides everything
```

Because `ASPNETCORE_ENVIRONMENT=Development`, the app merges `appsettings.Development.json`
on top of `appsettings.json`. When running in Docker it will be `Production`, so
`appsettings.Production.json` is merged instead.

> **Discussion:** Open both `appsettings.Development.json` and `appsettings.Production.json`.
> What is different between them? What stays the same?

---

## Step 2 — Find the Bug: Wrong Database Provider

The app currently uses SQLite in both dev and production.
That works fine locally, but production uses PostgreSQL — and SQLite cannot parse a
PostgreSQL connection string.

Open `src/LightningLab3/Program.cs` and find the TODO:

```csharp
// TODO (Lab - Step 4): This always uses SQLite — even when running in Docker against PostgreSQL.
// Fix this by switching the database provider based on the current environment.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));
```

**Don't fix it yet.** First, watch it fail.

### Create your .env file

```bash
cp .env.example .env
```

Open `.env` — the default `DB_PASSWORD=devpassword` is fine for local use.

### Run in Docker and observe the crash

```bash
docker compose up --build
```

Watch the logs. The app will crash with an error similar to:

```
Unhandled exception. System.ArgumentException: Connection string keyword 'host' is not supported.
   at Microsoft.Data.Sqlite.SqliteConnectionStringBuilder...
   at Program.<Main>$(String[] args) in Program.cs:line 25
```

SQLite is trying to parse the PostgreSQL connection string (`Host=db;Database=...`) and
does not recognise the `host` keyword — because that is a PostgreSQL concept, not SQLite.
That is the bug.

> **Discussion:** Trace why Docker gets a different connection string than `dotnet run`.
> Start at `docker-compose.yml` → `ConnectionStrings__DefaultConnection` →
> `appsettings.Production.json` → `appsettings.json` → `Program.cs`.
> (Note: `__` double-underscore in env var names maps to `:` in .NET config keys.)

---

## Step 3 — Fix It and Run in Production

Replace the single hardcoded `AddDbContext` call with an environment check:

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connectionString));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
}
```

Run in Docker again:

```bash
docker compose up --build
```

Wait for the `db` health check to pass, then open **http://localhost:8080/games**.

You should now see a **green banner** showing:

```
ASPNETCORE_ENVIRONMENT   Production
Database provider        PostgreSQL (Docker container)
```

```bash
docker compose down
```

> **Discussion:** You changed two lines of code. The connection string itself did not change.
> What actually caused the app to switch databases?

---

## Step 4 — Compare the Two Environments

| Concern | Development (`dotnet run`) | Production (`docker compose up`) |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Production` |
| Set by | `launchSettings.json` | `docker-compose.yml` |
| Database provider | SQLite | PostgreSQL |
| Connection string source | `appsettings.Development.json` | `docker-compose.yml` env var |
| Config file loaded | `appsettings.Development.json` | `appsettings.Production.json` |
| Secrets in any committed file | None | None |

> **Key insight:** The same codebase, with zero code changes at runtime, behaves
> differently in each environment because configuration is injected from outside
> the application — not baked into the code.

---

## The Two Rules

1. **Use environment-specific config files for non-secret settings.**
   `appsettings.Development.json` for local paths and dev-only logging.
   `appsettings.Production.json` for production log levels and feature flags.

2. **Inject environment identity from outside the codebase.**
   `launchSettings.json` for local dev (not deployed).
   Environment variables (from Docker or your cloud provider) everywhere else.

---

## Beyond This Lab — CI/CD Pipelines and Real Production

In this lab, `docker-compose.yml` plays the role of "the thing that sets environment variables."
In real production, that role belongs to your CI/CD pipeline and cloud platform.
Your `Program.cs` does not change — it still just reads from `builder.Configuration`.
Only who injects the variables changes.

### CI/CD Pipeline (GitHub Actions)

A pipeline knows which environment to target because you configure it explicitly.
Secrets are stored in the platform's vault — never in the repo.

```yaml
# .github/workflows/deploy.yml

jobs:
  deploy-staging:
    environment: staging
    steps:
      - name: Deploy to staging
        env:
          ASPNETCORE_ENVIRONMENT: Staging
          ConnectionStrings__DefaultConnection: ${{ secrets.STAGING_DB_CONNECTION }}
        run: ./deploy.sh

  deploy-production:
    environment: production
    steps:
      - name: Deploy to production
        env:
          ASPNETCORE_ENVIRONMENT: Production
          ConnectionStrings__DefaultConnection: ${{ secrets.PROD_DB_CONNECTION }}
        run: ./deploy.sh
```

`secrets.STAGING_DB_CONNECTION` and `secrets.PROD_DB_CONNECTION` are stored in
GitHub's secrets vault (repo → Settings → Secrets and variables → Actions).
They are injected at runtime and never appear in the repo or logs.

A typical flow looks like this:

```
push to main
  → build Docker image
  → deploy to staging   (ASPNETCORE_ENVIRONMENT=Staging,     staging secrets)
  → manual approval
  → deploy to production (ASPNETCORE_ENVIRONMENT=Production, production secrets)
```

The same Docker image is promoted through environments.
The image never changes — only the variables injected at deploy time change.

### Cloud Platforms

Each platform has its own way of storing and injecting variables,
but the result is always the same: env vars arrive in your container at startup.

**Azure App Service** — Portal → Configuration → Application Settings:
```
ASPNETCORE_ENVIRONMENT               = Production
ConnectionStrings__DefaultConnection = Host=prod.postgres.database.azure.com;...
```
Sensitive values can reference Azure Key Vault directly so the password never
appears in the portal UI:
```
@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/db-connection)
```

**AWS ECS** — Task definition references Secrets Manager:
```json
"secrets": [
  {
    "name": "ConnectionStrings__DefaultConnection",
    "valueFrom": "arn:aws:secretsmanager:us-east-1:123:secret:prod/db"
  }
]
```
AWS fetches the secret at container startup and injects it as an env var.

**Kubernetes** — A `Secret` object in the cluster, referenced by the deployment:
```yaml
env:
  - name: ConnectionStrings__DefaultConnection
    valueFrom:
      secretKeyRef:
        name: db-secret
        key: connection-string
```

### Where secrets live across environments

| Environment | Who sets the variables | Where secrets are stored |
|---|---|---|
| Local dev | `docker-compose.yml` reads `.env` | `.env` file (never committed) |
| CI/CD pipeline | GitHub Actions job config | GitHub repository secrets |
| Azure | App Service Application Settings | Azure Key Vault |
| AWS | ECS task definition | AWS Secrets Manager |
| Kubernetes | Deployment YAML | Kubernetes Secret objects |

In every case, `Program.cs` calls `builder.Configuration.GetConnectionString(...)`
and has no idea which platform put the value there.
That separation is what makes the same codebase deployable anywhere.

---

## Instructor Notes

### What to emphasize

- **Draw the config loading chain on a whiteboard before students start.**
  Many students assume `appsettings.Development.json` *replaces* `appsettings.json`.
  It does not — it *merges* and overrides on a key-by-key basis.

- **The `__` double-underscore syntax** in `docker-compose.yml`.
  `ConnectionStrings__DefaultConnection` maps to `ConnectionStrings:DefaultConnection` in JSON.
  Students who use a single underscore will get a "connection string not found" crash.

- **`launchSettings.json` is never deployed.**
  This is why the environment variable must be set externally in Docker/cloud.
  Ask: "If launchSettings.json doesn't exist on the server, how does production know its environment?"

- **Point out `Program.cs:31-34`** — the `IsDevelopment()` check for the error handler
  is already there and working before students make any change. It is a free demonstration
  that the environment flag is already controlling behavior.

### Common mistakes

| Mistake | Symptom | Fix |
|---|---|---|
| Single underscore in env var names | Config key not found at runtime | Remind them: `__` maps to `:` in .NET config |
| Not copying `.env.example` to `.env` | Docker crashes — `DB_PASSWORD` is empty | Run `docker compose config` to inspect resolved env vars |
| Running `docker compose up` without `--build` after code changes | Old Docker image is used | Always use `docker compose up --build` |
| Adding `UseNpgsql` without fixing the `using` / package reference | Build error | `Npgsql.EntityFrameworkCore.PostgreSQL` is already in the `.csproj` |

### Discussion questions

- Why is `appsettings.Development.json` safe to commit, but `.env` is not?
- What would happen if you set `ASPNETCORE_ENVIRONMENT=Production` locally
  and ran `dotnet run`? Try it.
- In real production, `.env` would not exist on the server. Variables would be injected
  by your cloud platform — Azure App Service, AWS ECS, Kubernetes Secrets.
  The principle is identical: one env var controls everything.
- How would a CI/CD pipeline know which environment to target?
  *(See the Beyond This Lab section for examples.)*

### Verifying completion

Ask each student to show:
1. `dotnet run` → blue Development banner at http://localhost:5101/games
2. `docker compose up --build` → green Production banner at http://localhost:8080/games

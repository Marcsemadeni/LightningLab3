# Lightning Lab 3 - Keeping Secrets Secret

**Topic:** Environment Configuration and Secrets Management (Dev vs Production)
**Stack:** .NET 10 - Blazor Web App - EF Core - SQLite (dev) - PostgreSQL (prod) - Docker

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
├── src/LightningLab3/               <- Blazor Web App
│   ├── Components/Pages/Games.razor <- the page you will be fixing
│   ├── Data/AppDbContext.cs         <- EF Core context + seed data
│   ├── Models/Game.cs
│   ├── Services/RatingsService.cs   <- Bug 2 is here
│   ├── appsettings.json             <- Bug 1 is here
│   ├── appsettings.Development.json <- you will edit this in Step 2
│   ├── appsettings.Production.json  <- production config (no secrets)
│   ├── Program.cs                   <- Bug 3 is here
│   └── Dockerfile
├── tests/LightningLab3.Tests/
│   ├── SanityTests.cs               <- always green, confirms test runner works
│   ├── RatingsServiceTests.cs       <- red until you fix Bug 2 (Step 3)
│   └── KeyRotationTests.cs          <- red until you complete the Challenge
├── docker-compose.yml
├── .env.example                     <- copy this to .env and fill in values
└── README.md
```

---

## Step 1 - Clone and Run the Tests

Clone the repo, then run:

```bash
dotnet test
```

You should see:

```
Failed! -  Total: 7, Failed: 4, Succeeded: 3, Skipped: 0
```

The 4 failing tests are not accidents - they describe exactly what is broken.
The 3 passing tests confirm your environment works. **Your goal is to get all 7 tests green.**

> **Discussion:** Look at the failing test names before reading any code.
> What do they tell you about what is wrong?

---

## Step 2 - Find and Fix Bug 1: Wrong Connection String

### Observe the crash

Try to run the app:

```bash
dotnet run --project src/LightningLab3
```

You will get an error similar to:

```
SqliteException: unable to open database file
```

### Find the bug

Open `src/LightningLab3/appsettings.json`.
Look at the `ConnectionStrings` section. What is wrong with that path?

> **Discussion:** Why does this work on the original developer's machine but not yours?
> What would happen if ten developers each had a different machine path?
> What would happen if you deployed this to a server?

### Fix it

The correct fix is to **remove** the broken path from `appsettings.json` and add
an environment-specific override in `appsettings.Development.json`.

.NET loads config in layers:

```
appsettings.json                         <- base defaults (loaded always)
  └── appsettings.{Environment}.json     <- overrides for this specific environment
        └── environment variables        <- highest priority, overrides everything
```

**1.** In `appsettings.json`, remove (or comment out) the `ConnectionStrings` block.

**2.** Open `src/LightningLab3/appsettings.Development.json` and add a connection string
that uses a **relative path** (no hardcoded drive or username):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=games.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**3.** Run the app again:

```bash
dotnet run --project src/LightningLab3
```

Navigate to **http://localhost:5101/games** - you should see the Game Library page with data.
Notice the blue banner showing `Development` environment and `SQLite`.

> **Discussion:** Why is `appsettings.Development.json` safe to commit but `appsettings.json`
> with a hardcoded absolute path is not?

---

## Step 3 - Find and Fix Bug 2: Hardcoded API Key

### Observe the failing tests

```bash
dotnet test
```

The `RatingsServiceTests` still fail. Read the failure messages - they tell you exactly
which file to open and what to look for.

### Find the bug

Open `src/LightningLab3/Services/RatingsService.cs`.

You will find this near the top:

```csharp
private const string ApiKey = "sk-ratings-abc123-hardcoded";
```

The constructor receives `IConfiguration` but never uses it for the key.

> **Discussion:**
> - This key is now in git history. Even if you delete it in a future commit,
>   anyone can run `git log -p` and find it. How would you clean that up?
> - What if a teammate accidentally pushed this to a public GitHub repo?

### Fix it

**1.** Remove the `private const string ApiKey` line.

**2.** Update `GetApiKey()` to read from `IConfiguration`:

```csharp
public string GetApiKey() =>
    _configuration["RatingsApi:ApiKey"]
        ?? throw new InvalidOperationException("RatingsApi:ApiKey is not configured.");
```

**3.** Update `ValidateApiKey()` to call `GetApiKey()`:

```csharp
public bool ValidateApiKey(string key) => key == GetApiKey();
```

### Store the key with dotnet user-secrets

User secrets are stored **outside** the project directory in your OS user profile.
They never touch the git repo.

```bash
cd src/LightningLab3

# Initialize user-secrets for this project (only needed once per machine)
dotnet user-secrets init

# Set the development API key
dotnet user-secrets set "RatingsApi:ApiKey" "sk-dev-local-testing-key"
```

**4.** Run the tests:

```bash
dotnet test
```

Expected result:

```
Failed! -  Total: 7, Failed: 2, Succeeded: 5, Skipped: 0
```

**5.** Run the app and navigate to **http://localhost:5101/games**.
The "Ratings API key" row should now show **Configured**.

> **Discussion:** Where are user-secrets actually stored on disk?
> Run `dotnet user-secrets list` to see what is set.
> On Windows: `%APPDATA%\Microsoft\UserSecrets\{guid}\secrets.json`

---

## Step 4 - Find and Fix Bug 3: Wrong Database Provider in Docker

### Create your .env file

```bash
cp .env.example .env
```

Open `.env` and fill in the values:

```
DB_PASSWORD=devpassword
RATINGS_API_KEY=sk-dev-local-testing-key
```

> `.env` is in `.gitignore` and will never be committed.

### Observe the crash in Docker

```bash
docker compose up --build
```

Watch the logs. The app will crash - SQLite is trying to interpret a PostgreSQL
connection string as a file path.

> **Discussion:** Why does `ASPNETCORE_ENVIRONMENT=Production` cause a different connection
> string to be used? Trace the config loading order from `docker-compose.yml` through
> `appsettings.Production.json` to `Program.cs`.

### Find the bug

Open `src/LightningLab3/Program.cs`. You will find:

```csharp
// TODO (Lab - Step 4): This always uses SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));
```

### Fix it

Replace the single `AddDbContext` call with an environment check:

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

### Run in Docker

```bash
docker compose up --build
```

Wait for the `db` health check to pass, then open **http://localhost:8080/games**.

You should see a **green** banner showing `Production`, `PostgreSQL`, and the API key as **Configured**.

```bash
docker compose down
```

> **Discussion:** The seed data appears even in a fresh Postgres container.
> Where does it come from? (Hint: look at `AppDbContext.OnModelCreating` and
> `EnsureCreated` in `Program.cs`.)

---

## Step 5 - Compare the Two Environments

| Concern | Development (`dotnet run`) | Production (`docker compose up`) |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Production` |
| Database provider | SQLite | PostgreSQL |
| Connection string source | `appsettings.Development.json` | `docker-compose.yml` env var |
| API key source | `dotnet user-secrets` | `.env` -> `docker-compose.yml` env var |
| Secrets in any committed file | None | None |

> **Key insight:** The same codebase, with zero code changes, runs correctly in both
> environments because configuration is injected from the outside, not baked into the code.

---

## Challenge - Key Rotation

A security best practice is to rotate API keys regularly, or immediately when a key
is suspected to be compromised.

**With a hardcoded key:** edit source -> commit -> push -> redeploy

**With configuration-based secrets:** update one value -> restart the app

### Get the challenge tests green

```bash
dotnet test --filter KeyRotation
```

These tests verify:
1. After rotating to a new key, the new key is accepted and the old key is rejected
2. Dev and production can have different keys - a dev key leak does not compromise production

If you completed Step 3 correctly, these should already pass. Confirm:

```bash
dotnet test
```

```
Passed! -  Total: 7, Failed: 0, Succeeded: 7, Skipped: 0
```

### Simulate rotation without changing any code

**In development:**

```bash
dotnet user-secrets set "RatingsApi:ApiKey" "sk-dev-rotated-2025" --project src/LightningLab3
dotnet run --project src/LightningLab3
```

Navigate to the Game Library - the key badge still shows **Configured** with the new key's
last 4 characters. No code was touched.

**In production (Docker):**

```bash
docker compose down
# Edit .env, change RATINGS_API_KEY to a new value
docker compose up --build
```

New key in use. No code changes, no new commit, no new Docker image.

> **Discussion:** In real production, `.env` would not exist on the server. Variables
> would be injected by your cloud platform - Azure App Service, AWS ECS, Kubernetes Secrets.
> The principle is identical.

---

## The Three Rules

1. **Never hardcode secrets in source code.** They end up in git history forever.

2. **Use environment-specific config files for non-secret settings.**
   `appsettings.Development.json` for local paths, `appsettings.Production.json` for
   production log levels and feature flags.

3. **Inject secrets from outside the codebase.**
   `dotnet user-secrets` for local development.
   Environment variables (from Docker or your cloud provider) everywhere else.

---

## Instructor Notes

### What to emphasize

- **The `appsettings` layering order** - draw it on a whiteboard before students start.
  Many students assume `appsettings.Development.json` completely replaces `appsettings.json`,
  when it actually merges and overrides on a key-by-key basis.

- **Where user-secrets are stored** - show students the actual file on disk.
  On Windows: `%APPDATA%\Microsoft\UserSecrets\{guid}\secrets.json`
  The GUID matches `<UserSecretsId>` in the `.csproj`. This is why secrets do not follow
  the repo when someone clones it on a new machine.

- **The double-underscore syntax** in `docker-compose.yml`.
  `ConnectionStrings__DefaultConnection` maps to `ConnectionStrings:DefaultConnection` in JSON.
  Students who use a single underscore will get a "connection string not found" crash.

### Common mistakes

| Mistake | Symptom | Fix |
|---|---|---|
| Editing `appsettings.json` instead of `appsettings.Development.json` | App works locally but Docker crashes with wrong path | Ask: "which appsettings file does Docker load?" |
| Forgetting `dotnet user-secrets init` | `set` command throws an error | Run `init` first - it adds `<UserSecretsId>` to the `.csproj` |
| Single underscore in env var names | Config key not found at runtime | Remind them: `__` maps to `:` in .NET config |
| Not copying `.env.example` to `.env` | Docker crashes - `RATINGS_API_KEY` is empty | Run `docker compose config` to inspect resolved env vars |
| Running `docker compose up` without `--build` after code changes | Old Docker image is used | Always use `docker compose up --build` |

### Discussion questions

- Why is `appsettings.Development.json` safe to commit, but `.env` is not?
- If a developer accidentally pushes a real API key to a public repo, what should they do?
  *(Rotate and revoke immediately. Deleting the commit is not enough - forks and caches may already have it.)*
- What is the difference between `dotnet user-secrets` and environment variables?
  When would you use one over the other?
- How would you handle secrets in a CI/CD pipeline?
  *(GitHub Actions secrets, Azure Key Vault, AWS Secrets Manager - same principle, different tool.)*

### Verifying completion

Ask each student to run `dotnet test` and show you the output.
All 7 green plus a running Docker container confirms every step is complete.

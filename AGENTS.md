# AGENTS.md

## Project purpose

NutritionTracker is an AI-assisted calorie and macronutrient tracking application. Users will describe foods, meals, and recipe changes in natural language. The LLM may interpret intent and select allowlisted tools, but it is never the source of truth.

The backend owns all validation, persistence, authorization, idempotency, and nutrition arithmetic. Food data, recipes, recipe versions, meal records, nutrition targets, chat messages, and processed commands belong in SQL storage. Calculations must be deterministic and reproducible from persisted inputs.

The repository currently contains the .NET backend foundation, domain model, deterministic `NutritionCalculator`, EF Core SQLite persistence, migrations, and tests. React/TypeScript and LLM integration are planned but are not present yet.

## Required pre-work reading

Before planning or changing anything:

1. Read `AGENTS.md` completely.
2. Read `docs/implementation-status.md` completely.
3. If `docs/implementation-status.md` is missing, say so in the work plan and establish the current state from the repository and recent Git history. Do not silently invent its contents or create it unless the task requests that.
4. Run `git status --short --branch` and preserve all existing user changes.
5. Inspect the relevant projects, tests, migrations, and recent commits before proposing new abstractions.

Repository contents are the source of truth when summaries or prior discussion conflict with the checked-in implementation. Report material conflicts before proceeding.

## Technology stack

- .NET 10, pinned by `global.json` to SDK `10.0.301` with latest-patch roll-forward.
- C# with nullable reference types and implicit usings enabled.
- ASP.NET Core Web API using Controllers and centralized `ProblemDetails` handling.
- Entity Framework Core `10.0.9` with SQLite.
- Local `dotnet-ef` `10.0.9`, declared in `dotnet-tools.json`.
- Swashbuckle/OpenAPI for development API documentation.
- xUnit for unit and integration tests.
- Microsoft.Extensions.Logging for application logging.
- React, TypeScript, and Vite are the planned frontend stack; no frontend project exists yet.

All projects treat compiler and recommended analyzer warnings as errors through `Directory.Build.props`. Do not suppress warnings or upgrade SDKs/packages unless the task requires it and the change is justified.

## Repository structure

```text
NutritionTracker.sln
Directory.Build.props
global.json
dotnet-tools.json

src/
  NutritionTracker.Domain/
    Chat/
    Commands/
    Common/
    Foods/
    Meals/
    Nutrition/
    Recipes/
    Users/
  NutritionTracker.Application/
  NutritionTracker.Infrastructure/
    Persistence/
      Configurations/
      Migrations/
  NutritionTracker.Api/
    Controllers/

tests/
  NutritionTracker.Domain.Tests/
  NutritionTracker.Application.Tests/
  NutritionTracker.IntegrationTests/
```

- `NutritionTracker.Domain` contains entities, value objects, invariants, enums, and deterministic nutrition calculations. It has no EF Core or ASP.NET Core dependency.
- `NutritionTracker.Application` is the use-case orchestration layer. It currently contains only its assembly marker and must remain independent of Infrastructure and API.
- `NutritionTracker.Infrastructure` contains EF Core, SQLite mappings, migrations, and dependency registration.
- `NutritionTracker.Api` is the HTTP and composition-root layer. Controllers must remain thin and must not contain business models or nutrition arithmetic.
- `NutritionTracker.Domain.Tests` covers domain invariants and calculations.
- `NutritionTracker.Application.Tests` protects Application dependency direction and will contain use-case tests.
- `NutritionTracker.IntegrationTests` exercises the API and migrations against isolated temporary SQLite databases.

## Architecture and dependency direction

Allowed production-project dependencies are:

```text
NutritionTracker.Domain          -> no project dependencies
NutritionTracker.Application     -> NutritionTracker.Domain
NutritionTracker.Infrastructure  -> NutritionTracker.Application + NutritionTracker.Domain
NutritionTracker.Api             -> NutritionTracker.Application + NutritionTracker.Infrastructure
```

Do not reverse these dependencies. In particular:

- Domain must never reference Application, Infrastructure, API, EF Core, or ASP.NET Core.
- Application must never reference Infrastructure or API.
- Infrastructure implements persistence and external adapters; it does not own business rules.
- API handles transport concerns and delegates use cases to Application.
- Frontend code must not calculate authoritative calories or macronutrients.
- LLM code must not calculate authoritative values, access SQL directly, invent entity IDs, or execute arbitrary operations.
- Every LLM-initiated mutation must pass through typed Application commands, backend validation, authorization, and idempotency controls.
- `NutritionCalculator` is the authoritative implementation of nutrition formulas. It uses `decimal`, performs no intermediate rounding, and rounds only through the explicit boundary operation used for API output or persisted snapshots.
- Historical meal values must use persisted nutrition snapshots so later food or recipe changes cannot rewrite history.

Use existing domain types and mappings before adding parallel abstractions. Explain any proposed architectural pattern that changes these boundaries.

## Build, test, lint, migration, and run commands

Run commands from the repository root.

Restore local tools and packages:

```powershell
dotnet tool restore
dotnet restore NutritionTracker.sln
```

Build with analyzers and warnings-as-errors:

```powershell
dotnet build NutritionTracker.sln --no-restore
```

Run all tests:

```powershell
dotnet test NutritionTracker.sln --no-build --no-restore
```

Verify formatting and code-style rules:

```powershell
dotnet format NutritionTracker.sln --verify-no-changes --no-restore
```

The build is also a required lint gate because analyzer warnings are errors. Fix warnings at their source; do not add `NoWarn` entries merely to pass verification.

Run the API:

```powershell
dotnet run --project src/NutritionTracker.Api/NutritionTracker.Api.csproj
```

In Development, Swagger is available at `/swagger` and the health endpoint at `/health`.

Create a migration only when the persistence model changes:

```powershell
dotnet tool run dotnet-ef migrations add <MigrationName> `
  --project src/NutritionTracker.Infrastructure/NutritionTracker.Infrastructure.csproj `
  --startup-project src/NutritionTracker.Api/NutritionTracker.Api.csproj `
  --output-dir Persistence/Migrations
```

Check migration/model synchronization:

```powershell
dotnet tool run dotnet-ef migrations has-pending-model-changes `
  --project src/NutritionTracker.Infrastructure/NutritionTracker.Infrastructure.csproj `
  --startup-project src/NutritionTracker.Api/NutritionTracker.Api.csproj
```

Do not run `EnsureCreated` for application databases. Integration tests must use unique temporary SQLite files and apply migrations.

## Git workflow

1. Inspect `git status --short --branch` before editing.
2. Work on the current branch unless the user explicitly requests a new branch.
3. Preserve unrelated modified and untracked files; never overwrite or stage them as part of another task.
4. Keep each commit limited to one logical, completed stage.
5. Review `git diff --check`, changed files, and verification results before committing.
6. Commit and push only when the current task or standing user instruction authorizes it. If authorization is absent, propose the commit message without committing.
7. Do not amend, rebase, force-push, or rewrite published history unless explicitly requested and the consequences are understood.

Never use destructive cleanup commands such as `git reset --hard`, `git clean -fd`, or checkout operations that discard user work.

## Conventional Commit rules

Use the form:

```text
<type>(optional-scope): <imperative summary>
```

Allowed common types:

- `feat`: new user-facing or domain capability.
- `fix`: defect correction.
- `refactor`: internal restructuring with no behavior change.
- `test`: test-only changes.
- `docs`: documentation-only changes.
- `build`: dependency, package, SDK, or build-system changes.
- `chore`: repository maintenance or scaffolding.
- `ci`: CI workflow changes.

Keep the summary concise, lowercase where natural, imperative, and without a trailing period. Add a body when the reason, migration impact, security tradeoff, or compatibility concern is not obvious.

Examples:

```text
feat: add deterministic nutrition calculator
fix(persistence): enforce idempotency key uniqueness
docs: document repository agent workflow
```

## Security and secret handling

- Never commit API keys, tokens, passwords, private connection strings, personal data, `.env` files containing secrets, or user-secret files.
- Keep OpenAI and other provider credentials in environment variables, .NET user secrets, or the deployment secret store.
- Override production connection strings through configuration, for example `ConnectionStrings__NutritionDatabase`; do not hardcode production credentials.
- Never commit SQLite database files, `-wal`/`-shm` files, `bin/`, `obj/`, test artifacts, logs containing user content, or frontend dependency/build directories.
- Treat chat messages and nutrition history as potentially sensitive personal data. Avoid logging full message content or nutrition records unless explicitly required and appropriately protected.
- Do not send secrets, database dumps, unrelated user data, or internal exception details to an LLM.
- Treat all LLM output and tool arguments as untrusted input. Validate schemas, entity ownership, ranges, units, and permissions in backend code.
- Never execute LLM-generated SQL or expose generic database/filesystem tools to the model.
- Use parameterized EF Core operations and keep mutation tools allowlisted and narrowly typed.
- Preserve the unique `(UserId, IdempotencyKey)` guarantee for processed commands.
- Investigate NuGet security warnings; do not suppress vulnerability warnings without an explicit, documented decision.

## Definition of done

A task is complete only when all applicable conditions are satisfied:

1. `docs/implementation-status.md` was read before work, or its absence was explicitly reported.
2. The implementation matches the requested scope without unrelated features or rewrites.
3. Architecture and dependency rules remain intact.
4. Domain invariants, deterministic arithmetic, authorization, and idempotency are enforced in backend code rather than LLM, frontend, or Controllers.
5. New behavior has focused unit tests; persistence and HTTP behavior have integration tests where appropriate.
6. `dotnet restore`, `dotnet build`, and `dotnet test` succeed with no hidden failures. Run the formatting check for C# changes.
7. EF model changes include reviewed migrations, temporary-database verification, and no pending model changes.
8. No secrets, personal data, local databases, build output, or unrelated user files are included.
9. `git diff --check` passes and the changed-file list has been reviewed.
10. Assumptions, errors encountered, test results, and skipped checks are reported honestly.
11. A suitable Conventional Commit message is provided; commit/push occurs only when authorized.
12. The working tree contains no accidental changes introduced by the task.

For documentation-only changes, executable checks may be skipped when they cannot be affected, but the diff and document contents must still be reviewed and the skipped checks must be stated.

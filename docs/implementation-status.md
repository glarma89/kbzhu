# Implementation Status

Last updated: 2026-07-23

## Project overview

NutritionTracker is an AI-assisted calorie and macronutrient tracking application. The intended user experience is natural-language food, meal, and recipe tracking, while the backend remains authoritative for validation, persistence, idempotency, and all nutrition arithmetic.

The checked-in repository currently contains the .NET backend foundation, domain model, deterministic nutrition calculator, EF Core SQLite persistence, an initial migration, and automated tests. React/TypeScript frontend work and LLM integration are planned but have not been implemented.

`README.md` does not currently exist.

## Current branch

- Branch: `main`
- Upstream: `origin/main`
- Latest synchronized implementation commit: `7d985c7`
- Repository guidance and this implementation checkpoint are maintained as tracked documentation.

## Completed stages

1. **Solution scaffold** - commit `8b7f97e` (`chore: scaffold .NET solution`)
   - Created the .NET 10 solution and production/test projects.
   - Added strict shared compiler/analyzer settings.
   - Added Controllers, `ProblemDetails`, Swagger/OpenAPI, and `/health`.

2. **Core domain model** - commit `7848eef` (`feat: add core nutrition domain model`)
   - Added user, target, food, recipe, meal, chat, and processed-command entities.
   - Added domain guards, enums, nutrition value object, snapshots, and invariant tests.

3. **EF Core SQLite persistence** - commit `a268c16` (`feat: add SQLite persistence with EF Core`)
   - Added `NutritionDbContext` and per-entity mappings.
   - Added explicit precision, indexes, foreign keys, delete behavior, and idempotency uniqueness.
   - Added initial migration and temporary-SQLite integration tests.

4. **Deterministic nutrition calculations** - commit `7d985c7` (`feat: add deterministic nutrition calculator`)
   - Added product, recipe-total, per-100-gram, portion, and recipe-fraction calculations.
   - Added explicit boundary rounding and calculation tests.

5. **Repository guidance and implementation checkpoint**
   - Added root `AGENTS.md` with architecture, verification, Git, security, and definition-of-done rules.
   - Added this repository-state document as the required starting point for future work.

No frontend, LLM integration, or application use cases have been completed.

## Current architecture

Production dependency direction:

```text
NutritionTracker.Domain          -> no project dependencies
NutritionTracker.Application     -> NutritionTracker.Domain
NutritionTracker.Infrastructure  -> NutritionTracker.Application + NutritionTracker.Domain
NutritionTracker.Api             -> NutritionTracker.Application + NutritionTracker.Infrastructure
```

Current responsibilities:

- `NutritionTracker.Domain`: entities, value objects, invariants, enums, and deterministic nutrition calculations.
- `NutritionTracker.Application`: reserved for use-case orchestration; currently contains only its assembly marker.
- `NutritionTracker.Infrastructure`: EF Core SQLite persistence, entity configurations, migrations, and DI registration.
- `NutritionTracker.Api`: composition root, Controllers, centralized error handling, Swagger/OpenAPI, and health endpoint.
- `NutritionTracker.Domain.Tests`: domain invariant and nutrition calculation tests.
- `NutritionTracker.Application.Tests`: dependency-direction test; application use-case tests do not exist yet.
- `NutritionTracker.IntegrationTests`: health/OpenAPI tests and migration-backed temporary SQLite tests.

Authoritative calculations belong to `NutritionCalculator`. Controllers, frontend code, and future LLM adapters must not perform or supply authoritative nutrition arithmetic.

## Implemented functionality

### Domain

- `UserProfile`
- `NutritionTarget`
- `FoodProduct`
- `Recipe` and `RecipeIngredient`
- `Meal` and `MealItem`
- `ChatMessage`
- `ProcessedCommand`
- `NutritionValues`
- `MealType` and `ChatRole`
- Positive-weight, non-negative-nutrition, UTC, required-text, and non-empty-`Guid` validation
- Recipe usability validation requiring at least one ingredient
- Meal-item source invariant: exactly one of product or recipe
- Persistable meal-item nutrition snapshots and recipe version snapshots

### Nutrition calculations

- Product nutrition for an arbitrary positive weight
- Full recipe nutrition from persisted products and ingredient weights
- Recipe nutrition per 100 grams when prepared weight is known
- Recipe portion nutrition by consumed grams when prepared weight is known
- Recipe fraction calculations such as `0.5` and `0.25`
- `decimal` arithmetic without intermediate rounding
- Explicit four-decimal boundary rounding using `MidpointRounding.AwayFromZero`

### API foundation

- ASP.NET Core Controllers
- Centralized `ProblemDetails` and status-code responses
- Swagger/OpenAPI in Development
- `GET /health`
- Infrastructure registration through dependency injection

No nutrition, food, recipe, meal, goal, chat, or LLM API endpoints exist yet.

## Database status

- Provider: Entity Framework Core `10.0.9` with SQLite.
- Context: `NutritionDbContext` in `NutritionTracker.Infrastructure`.
- Connection string key: `ConnectionStrings: NutritionDatabase`.
- Development value: `Data Source=nutrition-tracker.db` in API configuration.
- Local SQLite files, WAL files, and SHM files are ignored by Git.
- No SQLite database file was present in the repository at inspection time.
- Integration tests create unique temporary SQLite files, apply migrations, and remove the files afterward.
- `SQLitePCLRaw.bundle_e_sqlite3 3.0.4` is explicitly referenced to avoid the vulnerable older native SQLite dependency.

Configured indexes include:

- `FoodProduct.NormalizedName`
- `Recipe (UserId, Name)`
- `Meal (UserId, OccurredAt)`
- Unique `ProcessedCommand (UserId, IdempotencyKey)`

Delete behavior is explicit. Aggregate children (`RecipeIngredient`, `MealItem`) cascade from their parent aggregate, while food/recipe references are restricted and `MealItem.SourceMessageId` uses `SetNull`.

## Migrations status

- Initial migration: `20260722182615_InitialCreate`
- Model snapshot: `NutritionDbContextModelSnapshot`
- The migration was previously applied successfully to an isolated temporary SQLite database.
- The EF model was previously verified with `migrations has-pending-model-changes`; no pending changes were reported.
- The latest calculator commit did not change persistence entities or mappings.
- The application does not automatically apply migrations on startup; database migration remains an explicit development/deployment operation.

## Verification status

Latest recorded verification on 2026-07-23 for the implementation at commit `7d985c7`:

- `dotnet restore NutritionTracker.sln`: passed
- `dotnet build NutritionTracker.sln --no-restore`: passed with 0 warnings and 0 errors
- `dotnet test NutritionTracker.sln --no-build --no-restore`: passed, 38/38 tests
  - Domain tests: 30 passed
  - Application tests: 1 passed
  - Integration tests: 7 passed

The complete test suite was run again after the documentation was created and passed 38/38 tests. No executable code, project file, configuration, or migration has changed since the successful build.

## Known issues and incomplete areas

- `README.md` does not exist.
- The Application layer has no implemented commands, queries, handlers, or orchestration services.
- There are no food, recipe, meal, nutrition-target, summary, or chat API endpoints.
- Authentication and user authorization are not implemented.
- LLM client integration, tool schemas, tool dispatch, confirmation workflows, and ambiguity handling are not implemented.
- React/TypeScript/Vite frontend is not present.
- There is no external or seeded food-product catalog.
- PostgreSQL support has not been implemented; only portability has been considered in architecture and mappings.
- No CI workflow is present.

## Current task

Finalize the repository guidance and implementation-status checkpoint as one documentation-only commit without changing application code.

Status: completed.

## Next task

No implementation task is assigned. Wait for user review and explicit direction; do not begin the next application stage automatically.

## Latest verified commit

```text
7d985c7 feat: add deterministic nutrition calculator
```

This is the latest application implementation commit whose build and complete test suite were verified. The subsequent documentation-only checkpoint does not change executable code.

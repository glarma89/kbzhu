# Implementation Status

Last updated: 2026-07-23

## Project overview

NutritionTracker is an AI-assisted calorie and macronutrient tracking application. The intended user experience is natural-language food, meal, and recipe tracking, while the backend remains authoritative for validation, persistence, idempotency, and all nutrition arithmetic.

The repository currently contains the .NET backend foundation, domain model, deterministic nutrition calculator, EF Core SQLite persistence, food-product, versioned-recipe, and meal-journal Application use cases, REST endpoints, migrations, and automated tests. React/TypeScript frontend work and LLM integration are planned but have not been implemented.

`README.md` does not currently exist.

## Current branch

- Branch: `main`
- Upstream: `origin/main`
- Latest local implementation commit: `c09e5a6`
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

6. **Food-product Application layer and REST API** - commit `f815124` (`feat(foods): add food product application workflows and API`)
   - Added typed create, update, get, search, and candidate-finding use cases.
   - Added an Application-owned repository abstraction with an EF Core implementation.
   - Added normalized Unicode/whitespace-insensitive name search without automatic deduplication or merging.
   - Added user-scoped visibility and ownership rules, with personal products ranked before global products.
   - Added thin food-product REST endpoints and centralized Application exception mapping.
   - Added domain, Application unit, and migration-backed HTTP integration tests.

7. **Versioned recipe management** - commit `238221e` (`feat(recipes): add immutable recipe version history and API`)
   - Selected separate immutable `RecipeVersion` records rather than relying only on the mutable recipe row and MealItem snapshots.
   - Preserved the stable recipe identifier while recording every composition version, audit metadata, exact product weights, and product nutrition snapshots.
   - Kept `MealItem.RecipeVersion` and its nutrition snapshot and added a database FK to the exact persisted recipe version.
   - Added create, get, search, update, archive, total-nutrition, and portion-calculation use cases and REST endpoints.
   - Added a migration that backfills existing recipes and version ingredients before enforcing version foreign keys.

8. **Meal journal Application layer and REST API** - commit `c09e5a6` (`feat(meals): add time-zone-aware meal journal and daily summaries`)
   - Added idempotent food, recipe-portion, and recipe-fraction logging operations.
   - Added meal-item weight updates, moves, and deletion with current daily summaries in every mutation response.
   - Added user-time-zone daily boundaries, snapshot-based totals, effective daily targets, and negative remaining values for over-target days.
   - Added migration-backed UTC-millisecond meal occurrence storage and processed-command result metadata for replay responses.

No frontend or LLM integration has been completed.

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
- `NutritionTracker.Application`: food-product, recipe, and meal-journal commands, queries, results, validation, orchestration services, repository abstractions, and Application exceptions.
- `NutritionTracker.Infrastructure`: EF Core SQLite persistence, entity configurations, migrations, and DI registration.
- `NutritionTracker.Api`: composition root, Controllers, centralized error handling, Swagger/OpenAPI, and health endpoint.
- `NutritionTracker.Domain.Tests`: domain invariant and nutrition calculation tests.
- `NutritionTracker.Application.Tests`: dependency-direction and food/recipe use-case tests.
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
- Unicode FormKC food-name normalization with whitespace collapsing and invariant casing
- Controlled food-product updates that preserve identity and ownership
- Stable recipe aggregates with immutable version history
- Exact per-version ingredient weights and product nutrition snapshots
- Recipe change time, reason, and source audit metadata
- Archive metadata and rejection of archived recipes for new use or updates

### Application use cases

- `CreateFoodProduct`
- `UpdateFoodProduct`
- `GetFoodProductById`
- `SearchFoodProducts`
- `FindCandidatesByName`
- Input validation for identifiers, text lengths, result limits, per-100-gram nutrition ranges, and referenced users
- Separate records are preserved when normalized names match; no automatic merge or deduplication occurs
- Searches include only global and caller-owned products and rank caller-owned products first
- `CreateRecipe`
- `GetRecipe`
- `SearchRecipes`
- `UpdateRecipe` with an expected-version conflict check
- `ArchiveRecipe`
- `CalculateRecipeNutrition`
- `CalculateRecipePortion`
- Recipe ownership and ingredient-product visibility validation
- Historical version retrieval and deterministic recalculation from persisted snapshots
- `AddFoodToMeal`
- `AddRecipePortionToMeal`
- `AddRecipeFractionToMeal`
- `UpdateMealItemWeight`
- `DeleteMealItem`
- `MoveMealItem`
- `GetDailySummary`
- `GetMealsForDate`
- `GetRemainingDailyTargets`
- User-local date resolution through `UserProfile.TimeZone` without server-local time
- Idempotent mutation replay through persisted processed-command results

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
- `GET /api/foods`
- `GET /api/foods/candidates`
- `GET /api/foods/{id}`
- `POST /api/foods`
- `PUT /api/foods/{id}`
- `GET /api/recipes`
- `GET /api/recipes/{id}` with optional historical version selection
- `POST /api/recipes`
- `PUT /api/recipes/{id}`
- `POST /api/recipes/{id}/archive`
- `GET /api/recipes/{id}/nutrition`
- `GET /api/recipes/{id}/nutrition/portion`
- `POST /api/meals/items/food`
- `POST /api/meals/items/recipe`
- `PUT /api/meals/items/{id}` for either weight update or move
- `DELETE /api/meals/items/{id}`
- `GET /api/meals`
- `GET /api/daily-summary`
- Application validation and not-found failures returned as `ProblemDetails`

No nutrition-target mutation, chat, or LLM API endpoints exist yet.

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
- `Recipe (UserId, NormalizedName)`
- `Meal (UserId, OccurredAt)`
- Unique `ProcessedCommand (UserId, IdempotencyKey)`

Delete behavior is explicit. Aggregate children (`RecipeIngredient`, `MealItem`) cascade from their parent aggregate, while food/recipe references are restricted and `MealItem.SourceMessageId` uses `SetNull`.

Recipe versioning uses:

- `RecipeVersions` with composite key `(RecipeId, Version)`.
- `RecipeVersionIngredients` with exact weights and per-100-gram nutrition snapshots.
- A composite optional FK from `MealItem (RecipeId, RecipeVersion)` to the recorded version.
- Current recipe ingredients remain on `RecipeIngredients` for simple current-state editing and calculation workflows.
- `Meal.OccurredAt` is normalized to UTC and stored as Unix milliseconds so SQLite can perform indexed UTC range queries.
- `ProcessedCommand` stores its result item and user-local result date so repeats return the original operation identity and current daily totals.

## Migrations status

- Initial migration: `20260722182615_InitialCreate`
- Recipe history migration: `20260723121438_AddRecipeVersionHistory`
- Meal journal migration: `20260723133852_AddMealJournal`
- Model snapshot: `NutritionDbContextModelSnapshot`
- All migrations apply successfully to isolated temporary SQLite databases.
- The meal-journal migration preserves and converts legacy `OccurredAt` offset timestamps to UTC Unix milliseconds.
- The latest calculator commit did not change persistence entities or mappings.
- The recipe migration backfills legacy recipe/version data and product nutrition snapshots before adding the MealItem-to-version FK.
- The application does not automatically apply migrations on startup; database migration remains an explicit development/deployment operation.

## Verification status

Latest recorded verification on 2026-07-23 for commit `c09e5a6`:

- `dotnet tool restore`: passed
- `dotnet restore NutritionTracker.sln`: passed
- `dotnet build NutritionTracker.sln --no-restore`: passed with 0 warnings and 0 errors
- `dotnet test NutritionTracker.sln --no-build --no-restore`: passed, 72/72 tests
  - Domain tests: 37 passed
  - Application tests: 18 passed
  - Integration tests: 17 passed
- `dotnet format NutritionTracker.sln --verify-no-changes --no-restore`: passed
- `dotnet-ef migrations has-pending-model-changes`: passed; no model changes were reported

The first sandboxed package restore attempt was blocked by NuGet network restrictions. It was repeated with approved network access and completed successfully before the final build and test run.

## Known issues and incomplete areas

- `README.md` does not exist.
- Application use cases currently cover food products, recipes, and meal journaling with daily summaries.
- There are no nutrition-target mutation or chat API endpoints.
- Authentication and user authorization are not implemented.
- LLM client integration, tool schemas, tool dispatch, confirmation workflows, and ambiguity handling are not implemented.
- React/TypeScript/Vite frontend is not present.
- There is no external or seeded food-product catalog.
- PostgreSQL support has not been implemented; only portability has been considered in architecture and mappings.
- No CI workflow is present.

## Current task

No implementation task is currently assigned.

Status: the meal-journal stage is implemented, verified, committed, and synchronized with `origin/main`.

## Next task

Wait for the next explicitly authorized implementation stage.

## Latest verified commit

```text
c09e5a6 feat(meals): add time-zone-aware meal journal and daily summaries
```

This is the latest committed and verified implementation stage.

# Implementation Status

Last updated: 2026-07-23

## Project overview

NutritionTracker is an AI-assisted calorie and macronutrient tracking application. The intended user experience is natural-language food, meal, and recipe tracking, while the backend remains authoritative for validation, persistence, idempotency, and all nutrition arithmetic.

The repository currently contains the .NET backend foundation, domain model, deterministic nutrition calculator, EF Core SQLite persistence, food-product, versioned-recipe, and meal-journal Application use cases, authenticated REST endpoints, migrations, automated tests, a provider-independent language-model abstraction and agent loop, an OpenAI Responses API adapter, allowlisted tool dispatch, persisted user-message/tool-execution recovery, trusted claim-based user identity propagation, explicit chat confirmation/cancellation continuation, and a canonical Russian assistant system instruction. React/TypeScript frontend work and a concrete production identity provider are not implemented.

`README.md` does not currently exist.

## Current branch

- Branch: `main`
- Upstream: `origin/main`
- Current commit: `9722992` (`feat(chat): secure authenticated assistant workflows`)
- Current implementation stage: trusted authenticated identity, explicit chat confirmation continuation, and the Russian assistant instruction; committed and verified
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

9. **Strongly typed LLM tool contract** - commit `c6e1834` (`feat(chat): add recoverable LLM tool workflow`)
   - Added an allowlisted catalog for 15 food, recipe, diary, summary, recent-meal, and target tools.
   - Added typed argument and structured-result DTOs plus one typed handler interface per tool.
   - Added Draft 2020-12 JSON schemas with required fields, strict unknown-field rejection, ranges, enums, and mutually exclusive quantity/update modes.
   - Kept trusted user identity, idempotency keys, source-message identity, and confirmation evidence in a backend-supplied invocation context rather than model arguments.
   - Required explicit confirmation for food updates, recipe updates, historical diary changes, and diary deletion.
   - Added serialization and JSON validation tests without adding an OpenAI dependency or concrete tool dispatch.

10. **Persisted user-message processing state machine** - commit `c6e1834` (`feat(chat): add recoverable LLM tool workflow`)
   - Added `Received`, `Interpreting`, `AwaitingClarification`, `AwaitingConfirmation`, `Executing`, `Completed`, and `Failed` states with guarded domain transitions.
   - Added a provider-independent coordinator that separates interpretation from tool execution and validates interpreted arguments against the allowlisted tool contract.
   - Persisted the original `ChatMessage`, interpretation snapshot, clarification/confirmation state, prepared tool call, idempotency key, execution result, failure/retry state, and response-delivery marker.
   - Added atomic duplicate-delivery handling through unique `(UserId, DeliveryKey)` storage and stable per-message tool idempotency keys.
   - Persisted `Executing` before invoking a tool so recovery repeats the same idempotent operation after a crash rather than creating a second `MealItem`.
   - Kept completed tool results available for final-response recovery without re-executing the tool.
   - Added migration `20260723143351_AddUserMessageProcessing` and transition, scenario, recovery, and persistence tests.

11. **OpenAI Responses API chat integration** - commit `6f57745` (`feat(chat): integrate OpenAI tool-calling agent loop`)
   - Added Application-owned `ILanguageModelClient`, provider-neutral response/tool-call contracts, and a bounded multi-turn agent loop.
   - Added `POST /api/chat/messages` with duplicate-delivery recovery, executed-action reporting, clarification/confirmation fields, and authoritative daily summaries.
   - Added an OpenAI Responses API HTTP adapter with per-request and whole-loop timeouts, cancellation, bounded exponential retry for rate limits and transient failures, and `previous_response_id` continuation.
   - Registers all 15 allowlisted tools with strict JSON schemas transformed to the OpenAI strict subset while retaining backend typed validation and mutually exclusive-mode validation.
   - Added concrete allowlisted dispatch to existing food, recipe, and meal Application services; the model cannot select arbitrary methods or SQL.
   - Added transactional `ToolExecution` persistence keyed by `(UserId, IdempotencyKey)` with tool name, argument hash, and structured result, preventing mutation replay after ambiguous network failures.
   - Added fake-language-model integration tests for sequential calls, duplicate delivery, authoritative calorie output, strict schemas, and the maximum agent-loop limit.
   - The official `OpenAI` .NET package 2.12.0 was evaluated, but its Responses surface emits `OPENAI001` evaluation diagnostics. Because warnings are errors and repository policy forbids suppressions, the integration uses the documented Responses HTTP API behind `ILanguageModelClient` instead.

12. **Trusted identity and chat confirmation continuation** - commit `9722992` (`feat(chat): secure authenticated assistant workflows`)
   - Added an Application-owned current-user contract and a framework-neutral `sub` claim contract, resolved from validated claims only in the API layer.
   - Protected food, recipe, meal, and chat endpoints and removed caller-supplied `userId` values from request bodies and query strings.
   - Added an unconfigured production authentication scheme that rejects protected requests until a real provider is registered, plus a test-only authentication handler for migration-backed HTTP tests.
   - Added `POST /api/chat/messages/{messageId}/confirmation` for explicit confirmation or cancellation using the normal chat response contract.
   - Persists a canonical prepared-tool argument hash and verifies ownership, workflow state, allowlisted tool registration, confirmation requirement, arguments, hash, and the original idempotency key before execution.
   - Confirmation binds fresh backend-owned evidence and executes only the persisted prepared operation; cancellation persists a terminal structured result without invoking a tool.
   - Repeated confirmation and cancellation deliveries replay the persisted terminal response without executing a mutation twice.
   - Added migration `20260723161419_AddChatConfirmationHash`, focused Application tests, and authenticated migration-backed HTTP tests. Automated tests continue to use `FakeLanguageModelClient` only.

13. **Russian AI-assistant system instruction** - commit `9722992` (`feat(chat): secure authenticated assistant workflows`)
   - Replaced the short inline agent instruction with an Application-owned canonical instruction used by every language-model request.
   - Requires Russian natural-language understanding, tool-authoritative mutation status and nutrition values, candidate selection, minimal clarification, intent separation, one-off recipe handling, confirmation gates, user-time-zone handling, and concise backend-derived summaries.
   - Explicitly resists prompt injection, arbitrary SQL/database access, tool-policy bypass, secret disclosure, invented identifiers, and invented nutrition data.
   - Keeps user products, recipes, and diary content out of the system instruction; those records may enter model context only through registered tools and only as needed.
   - Added a documentation specification with ten synthetic positive/negative dialogue scenarios and a focused Application test protecting the instruction's critical rules.

No frontend or concrete production identity-provider adapter has been completed. Automated tests never invoke OpenAI.

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
- `NutritionTracker.Application`: food-product, recipe, meal-journal, and chat commands, queries, results, validation, orchestration services, repository abstractions, the current-user and claim contracts, Application exceptions, provider-independent LLM/tool contracts, and the bounded agent loop.
- `NutritionTracker.Infrastructure`: EF Core SQLite persistence, allowlisted tool dispatch, the OpenAI Responses HTTP adapter, entity configurations, migrations, and DI registration.
- `NutritionTracker.Api`: composition root, claims-based current-user resolution, authentication/authorization middleware, Controllers, centralized error handling, Swagger/OpenAPI, and health endpoint.
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
- `UserMessageProcessing` and `MessageProcessingState`
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
- Persisted user-message processing transitions with retry-from-state recovery
- Critical ambiguity and missing required data block tool execution while preserving the original message
- Explicit confirmation gates dangerous tool calls; cancellation completes without executing a mutation
- Duplicate message deliveries are identified by a client delivery key rather than message text
- Stable per-message mutation idempotency keys survive retries and lost tool responses
- Completed structured tool results remain pending until the assistant response is marked delivered
- Provider-independent `ILanguageModelClient` with sequential tool-call continuation
- Bounded agent iterations, whole-loop cancellation/timeout, and persisted duplicate-delivery responses
- Trusted authenticated user identity from the validated `sub` claim; HTTP payloads cannot select another user
- Explicit pending-confirmation continuation with canonical argument binding and terminal cancellation
- Canonical Russian assistant system instruction applied to every provider request

### Future LLM tool contract

- Allowlisted definitions for `search_foods`, `get_food`, `create_food`, `update_food`, `search_recipes`, `get_recipe`, `create_recipe`, `update_recipe`, `add_food_to_diary`, `add_recipe_to_diary`, `update_diary_item`, `delete_diary_item`, `get_daily_summary`, `get_recent_meals`, and `get_nutrition_targets`
- Strict snake-case JSON serialization and rejection of unmapped properties, numeric enum values, invalid identifiers, invalid ranges, and invalid mutually exclusive argument combinations
- Model arguments exclude trusted user identity, idempotency keys, confirmation state, source-message identity, and authoritative diary nutrition values
- Food label nutrition is accepted only by `create_food` and `update_food` and remains subject to backend validation
- Diary additions accept only persisted entity identifiers, weight or recipe fraction, occurrence time, meal type, and captured user intent
- Structured success/error envelopes and typed result DTOs; tools never return composed assistant prose
- Search results expose `requires_selection` so multiple food or recipe matches are not automatically selected
- Backend-supplied confirmation evidence is bound to the tool name and canonical argument hash
- Mutating tool definitions require a backend-supplied idempotency key; read tools are explicitly non-mutating
- Provider-independent typed handler interfaces remain available; the concrete executor uses one explicit allowlisted dispatcher over existing Application services
- Concrete dispatch is restricted to the 15 registered definitions and validates every model argument again before invoking an Application service
- Strict model schemas require every property and represent optional values as nullable, while backend validation enforces richer invariants such as mutually exclusive quantity modes

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
- `POST /api/chat/messages`
- `POST /api/chat/messages/{messageId}/confirmation`
- Application validation and not-found failures returned as `ProblemDetails`

Food, recipe, meal, and chat endpoints require the `AuthenticatedUser` authorization policy. No production identity provider is configured yet, so protected endpoints reject requests until one supplies a validated non-empty GUID `sub` claim. No nutrition-target mutation endpoint exists. Chat uses the tool contract through the OpenAI Responses API adapter or a test fake.

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
- Unique `UserMessageProcessing (UserId, DeliveryKey)`
- Unique filtered `UserMessageProcessing (UserId, IdempotencyKey)` for prepared mutations
- Unique `ToolExecution (UserId, IdempotencyKey)` for committed model-initiated mutations

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
- User-message processing migration: `20260723143351_AddUserMessageProcessing`
- Chat tool-execution migration: `20260723151937_AddChatToolExecution`
- Chat confirmation-hash migration: `20260723161419_AddChatConfirmationHash`
- Model snapshot: `NutritionDbContextModelSnapshot`
- All six migrations apply successfully to isolated temporary SQLite databases.
- The meal-journal migration preserves and converts legacy `OccurredAt` offset timestamps to UTC Unix milliseconds.
- The latest calculator commit did not change persistence entities or mappings.
- The recipe migration backfills legacy recipe/version data and product nutrition snapshots before adding the MealItem-to-version FK.
- The application does not automatically apply migrations on startup; database migration remains an explicit development/deployment operation.
- The message-processing migration adds a one-to-one workflow row for each processed user `ChatMessage`, state-dependent check constraints, and duplicate-delivery/idempotency indexes.

## Verification status

Implementation-stage verification on 2026-07-23 for the Russian AI-assistant system instruction:

- `dotnet restore NutritionTracker.sln`: passed; all projects were up to date
- `dotnet build NutritionTracker.sln --no-restore`: passed with 0 warnings and 0 errors
- `dotnet test NutritionTracker.sln --no-build --no-restore`: passed, 113/113 tests
  - Domain tests: 43 passed
  - Application tests: 45 passed
  - Integration tests: 25 passed
- `dotnet format NutritionTracker.sln --verify-no-changes --no-restore`: passed
- `git diff --check`: passed
- Automated tests use fake language-model clients; no real OpenAI request was made

Implementation-stage verification on 2026-07-23 for trusted identity and chat confirmation continuation:

- `dotnet restore NutritionTracker.sln`: passed after approved NuGet network access; the sandboxed attempt was blocked during repository-signature retrieval
- `dotnet build NutritionTracker.sln --no-restore`: passed with 0 warnings and 0 errors
- `dotnet test NutritionTracker.sln --no-build --no-restore`: passed, 112/112 tests
  - Domain tests: 43 passed
  - Application tests: 44 passed
  - Integration tests: 25 passed
- `dotnet format NutritionTracker.sln --verify-no-changes --no-restore`: passed
- `dotnet-ef migrations has-pending-model-changes`: passed; no model changes were reported
- All HTTP integration tests use the test authentication handler and `FakeLanguageModelClient`; no real OpenAI request was made

Implementation-stage verification on 2026-07-23 for OpenAI chat integration:

- `dotnet restore NutritionTracker.sln`: passed
- `dotnet build NutritionTracker.sln --no-restore`: passed with 0 warnings and 0 errors
- `dotnet test NutritionTracker.sln --no-build --no-restore`: passed, 106/106 tests
  - Domain tests: 43 passed
  - Application tests: 41 passed
  - Integration tests: 22 passed
- `dotnet format NutritionTracker.sln --verify-no-changes --no-restore`: passed
- `dotnet-ef migrations has-pending-model-changes`: passed; no model changes were reported
- Integration tests use `FakeLanguageModelClient`; no real OpenAI request was made

Implementation-stage verification on 2026-07-23 for the tool-contract and user-message-processing changes:

- `dotnet tool restore`: passed
- `dotnet restore NutritionTracker.sln`: passed
- `dotnet build NutritionTracker.sln --no-restore`: passed with 0 warnings and 0 errors
- `dotnet test NutritionTracker.sln --no-build --no-restore`: passed, 103/103 tests
  - Domain tests: 43 passed
  - Application tests: 41 passed
  - Integration tests: 19 passed
- `dotnet format NutritionTracker.sln --verify-no-changes --no-restore`: passed
- Migration-backed integration tests applied all four migrations to isolated temporary SQLite databases
- `dotnet-ef migrations has-pending-model-changes`: passed; no model changes were reported

Sandboxed NuGet/tool access required approved network execution. The first migration attempt also exposed a premature coordinator DI registration without concrete interpreter/executor adapters; the registration was removed, and migration generation and all final checks then passed.

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
- Application use cases cover food products, recipes, meal journaling, daily summaries, and chat agent-loop orchestration.
- There is no nutrition-target mutation endpoint.
- A concrete production identity provider is not configured; the API currently defines the claims contract and intentionally rejects protected requests through its unconfigured authentication scheme.
- React/TypeScript/Vite frontend is not present.
- There is no external or seeded food-product catalog.
- PostgreSQL support has not been implemented; only portability has been considered in architecture and mappings.
- No CI workflow is present.

## Current task

No implementation task is currently active. The latest authenticated-assistant workflow stage is committed and verified.

## Next task

At the next authorized stage, future work may add automated multi-turn prompt-behavior evaluations, integrate a concrete production identity provider, or add nutrition-target mutation workflows.

## Latest verified commit

```text
9722992 feat(chat): secure authenticated assistant workflows
```

This commit includes trusted identity propagation, explicit confirmation/cancellation continuation, the confirmation-hash migration, the Russian system instruction, documentation, and the 113-test verified suite.

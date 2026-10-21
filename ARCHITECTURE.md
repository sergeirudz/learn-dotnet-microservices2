# API Architecture Patterns — reference for building .NET microservices

A catalog of the common ways to structure an API, what this course uses and why, and when to reach for each. Written so
that when starting a new service (in this project or a real one), the choice of pattern is a deliberate decision, not a
default.

File paths are relative to repo root. The finished course project lives at
`/Users/sergeirudz/Documents/GitHub/dotnet/learn-microservices2-finished/src`.

---

## 1. What this course uses: Clean Architecture layering + Vertical Slices + CQRS-lite

Three ideas stacked together (they are not alternatives to each other — they solve different problems):

| Idea                            | What it decides                                                                           | Where visible in repo                                                                        |
|---------------------------------|-------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------|
| **Clean Architecture layering** | Which *project* code lives in, and which direction dependencies may point                 | `Submission.Api` / `Submission.Application` / `Submission.Domain` / `Submission.Persistence` |
| **Vertical slices**             | How code is *organized inside* the Application layer — by feature, not by technical role  | `Submission.Application/Features/CreateArticle/`                                             |
| **CQRS-lite**                   | That reads and writes are *separate request types* with separate handlers (same database) | `CreateArticleCommand` (write) vs future `GetArticleQuery` (read)                            |

### 1.1 The full request flow, starting from the Endpoint

```
HTTP POST /api/articles
  │
  ▼
CreateArticleEndpoint                     src/Articles/Services/Submission/Submission.Api/Endpoints/CreateArticleEndpoint.cs
  │  routing, auth policy (RequireRole), binding JSON → command, HTTP status mapping
  │  NO business logic here — it is thin by design
  ▼
CreateArticleCommand                      .../Submission.Application/Features/CreateArticle/CreateArticleCommand.cs
  │  an immutable record; doubles as the public request contract
  ▼
ISender.Send(command)  →  MediatR pipeline (in-process, no network)
  │
  ▼
ValidationBehavior                        src/Articles/BuildingBlocks/Blocks.MediatR/Behaviors/ValidationBehavior.cs
  │  FluentValidation runs before the handler; throws ValidationException → 400
  ▼
SetUserIdBehavior                         .../Blocks.MediatR/Behaviors/SetUserIdBehavior.cs
  │  stamps audit fields (CreatedById) on commands implementing IAuditableAction
  ▼
CreateArticleCommandHandler               .../Features/CreateArticle/CreateArticleCommandHandler.cs
  │  orchestrates: load aggregate → call domain method → save
  ▼
Journal.CreateArticle(...)                src/Articles/Services/Submission/Submission.Domain/Entities/Journal.cs
  │  the business rules live HERE — on the aggregate, not in the handler
  ▼
Repository → SubmissionDbContext          .../Submission.Persistence/
  │  SaveChangesAsync; a DispatchDomainEventsInterceptor publishes domain events
  ▼
IdResponse                                src/Articles/BuildingBlocks/Articles.Abstractions/IdResponse.cs
  → 201 Created with the new id
```

The essential point of the pattern: **every box knows only its neighbor.** The endpoint knows the command + `ISender`,
nothing else. The handler knows the aggregate, nothing about HTTP. The aggregate knows nothing about persistence. That's
the Clean Architecture dependency rule showing up as code.

### 1.2 Anatomy of one vertical slice

One feature = one folder containing everything that feature needs:

```
Submission.Application/Features/
├── _Shared/
│   └── ArticleCommand.cs                 ← shared command base (audit fields, ArticleId)
├── CreateArticle/
│   ├── CreateArticleCommand.cs           ← request record
│   └── CreateArticleCommandHandler.cs    ← the use case
├── AssignAuthor/
│   ├── AssignAuthorCommand.cs
│   └── AssignAuthorCommandHandler.cs
├── ApproveArticle/
│   ├── ApproveArticleCommand.cs
│   ├── ApproveArticleCommandHandler.cs
│   └── PublishArticleApprovedEventHandler.cs   ← publishes integration event after success
└── UploadFiles/
    ├── _Shared/                          ← shared helpers for this group
    ├── UploadManuscriptFile/
    └── UploadSupplementaryFile/
```

Properties of the slice:

- **Adding a feature = adding a folder.** Handlers are discovered by assembly scanning
  (`config.RegisterServicesFromAssembly(...)` in `Submission.Application/DependencyInjection.cs`), so there is no
  central list to update.
- **Handler is `internal`** — nothing outside the Application assembly calls it directly; communication goes through
  `ISender`. See the handler in
  `Features/CreateArticle/CreateArticleCommandHandler.cs`.
- **Thin handlers, rich aggregates.** The handler is a use-case coordinator:
  load → `journal.CreateArticle(...)` → save. Rules belong to the domain. This is the repo guardrail "no service layer
  classes" in practice.
- The slice style also applies to event consumers (`PublishArticleApprovedEventHandler.cs` in the `ApproveArticle`
  slice) — a feature can own its reaction to events, not just its endpoint.

### 1.3 The layer structure of a service

```
Submission/
├── Submission.Api              ← presentation: endpoints, auth, middleware. Depends on Application
├── Submission.Application      ← use cases: Features/, DI registration. Depends on Domain
├── Submission.Domain           ← Entities, ValueObjects, Events — depends on NOTHING internal
└── Submission.Persistence      ← DbContext, Repositories, EntityConfigurations. Depends on Domain
```

Dependencies point inward only. The Domain project has no reference to EF Core or ASP.NET — which is why `Journal` can
enforce invariants in plain C# without knowing how it gets saved. The shared plumbing this relies on lives in
`src/Articles/BuildingBlocks/` (`Blocks.MediatR`,
`Blocks.EntityFrameworkCore`, `Blocks.Exceptions`, …) — the cross-cutting version of the same layering.

### 1.4 Two endpoint styles coexist in this repo

Both are thin entry points into the same CQRS core — the style is presentation-only:

```csharp
// Minimal API — used in Submission.Api
// src/Articles/Services/Submission/Submission.Api/Endpoints/CreateArticleEndpoint.cs
public static class CreateArticleEndpoint
{
    public static void Map(this IEndpointRouteBuilder app)
    {
        app.MapPost("api/articles", async (CreateArticleCommand command, ISender sender) =>
        {
            var response = await sender.Send(command);
            return Results.Created($"/api/articles/{response.Id}", response);
        })
        .RequireAuthorization(policy => policy.RequireRole(Role.AUT));
    }
}
```

```csharp
// FastEndpoints — used in Auth.API (class-per-endpoint, follows the REPR pattern)
// src/Articles/Services/Auth/Auth.API/Features/Users/Login/LoginEndpoint.cs
[HttpPost("/tokens")]
public class LoginEndpoint(PersonRepository _personRepository, ...)
    : Endpoint<LoginCommand, LoginResponse>
{
    public override async Task HandleAsync(LoginCommand command, CancellationToken ct) { ... }
}
```

Note the Auth endpoint is *fatter* — it orchestrates Identity + token creation directly. That is a reasonable trade for
a service whose logic is mostly infrastructure (auth is a technical concern, not a domain model). Pattern choice can
vary **per service**, as long as the layering discipline holds.

---

## 2. The classic alternative: Endpoint → DTO → Service

The most widespread pattern in older ASP.NET codebases and tutorials. It has several names:

| Name                                    | Emphasis                                                                    |
|-----------------------------------------|-----------------------------------------------------------------------------|
| **Service Layer** (Fowler, PoEAA)       | A layer orchestrating use cases between endpoint and persistence            |
| **Transaction Script** (Fowler)         | Each service method is one procedural block: validate → mutate → save       |
| **Controller-Service-Repository (CSR)** | The three-tier ASP.NET shape: `Controllers/`, `Services/`, `Repositories/`  |
| **N-tier / 3-tier**                     | Presentation → Business → Data as horizontal layers                         |
| **Anemic Domain Model**                 | The usual companion: entities are data bags, all behavior lives in services |

### The shape

```
HTTP POST /api/articles
  → binds ArticleDto                       (one DTO shared by create/update/etc.)
  → ArticleService.CreateArticle(dto)      (one class per entity, many methods)
      • validation                          (often manual, inside the method)
      • map DTO → entity
      • business rules HERE, procedural
      • _repository.Save()
      • map entity → response DTO
  → return 200
```

### Side-by-side with the course pattern

|                                   | Endpoint → DTO → Service                                    | CQRS + rich domain (this course)                                                                  |
|-----------------------------------|-------------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| Grouping                          | One class **per entity** (`ArticleService` with 10 methods) | One handler **per use case** (`CreateArticleCommandHandler`)                                      |
| Business rules                    | Inside service methods, procedural                          | On the aggregates (`journal.CreateArticle(...)`)                                                  |
| Entities                          | Data bags (anemic)                                          | Behavior-owning (`Users.AddRole(...)` in `src/Articles/Services/Auth/Auth.Domain/Users/Users.cs`) |
| Request shape                     | Shared CRUD DTOs (`ArticleDto` for create *and* update)     | Purpose-built record per operation                                                                |
| Validation                        | Manual in the service, or scattered                         | Declarative (`FluentValidation` validators + pipeline behavior)                                   |
| Adding a feature                  | Edit the existing service class (merge-conflict magnet)     | Add a folder; nothing else changes                                                                |
| Cross-cutting (audit, validation) | Repeated per method, or base-class hacks                    | Pipeline behaviors, applied by interface constraint                                               |

### When the service pattern is the *right* choice

Not a wrong pattern — it is the best fit when there is barely any domain logic:

- CRUD-heavy apps, admin panels, internal tools, BFFs aggregating other APIs.
- Reports/exports: load, transform, write — Transaction Script is the honest name for that.
- Small services in a microservice system where a rich domain model is overkill. Note how the Auth endpoint above leans
  this way, and the course's `EmailService`/`FileService`
  modules (`src/Articles/Modules/`) are pure infrastructure with no domain model at all.

The failure mode to recognize in other people's code: a service layer that *absorbed* rules that belong on entities
(anemic model) while the domain grew complex — then every rule is procedural code duplicated across service methods, and
nothing enforces invariants.

Naming trap: DDD also has **Domain Services** — logic spanning several aggregates that fits nowhere else. Different from
the *application* service in Endpoint→Service. Application service = use-case orchestration (in this repo, that role is
played by the handlers).

---

## 3. Other patterns worth knowing

### 3.1 Layered (horizontal) N-tier

The granddaddy: one project, folders by technical role.

```
MyApp/
├── Controllers/
├── Services/
├── Repositories/
├── Models/            (entities + DTOs mixed together)
└── Data/
```

Simple, familiar, degrades with size: the "Models" folder becomes a dumping ground, features are smeared across four
folders, and nothing stops a controller from touching a repository directly. Fine for small tools; this repo
deliberately rejects it (no god folders).

### 3.2 Clean Architecture / Onion / Hexagonal (Ports & Adapters)

These three names describe the same core idea with different diagrams: **the domain is at the center, everything else
depends on it, it depends on nothing.** External concerns (HTTP, EF, RabbitMQ) are "adapters" plugged into "ports"
(interfaces the domain defines).

- The repo's `.Api/.Application/.Domain/.Persistence` split **is** a Clean Architecture layering.
- Hexagonal's vocabulary is most useful when the same core serves multiple transports — e.g. the Auth service exposing
  **both** REST (FastEndpoints) **and** gRPC (`Auth.API/Features/Persons/PersonGrpcService.cs`) from one core.
- Cost: more projects and mapping code. Worth it when domain logic is complex; ceremony when it isn't.

### 3.3 REPR (Request-Endpoint-Response)

The pattern FastEndpoints is built around: one endpoint class per API operation, each owning its Request type, its
Response type, and its handling. Removes the "controller" as a concept — no more `ArticlesController` with 15 actions.
The Auth service's `Features/**/*Endpoint.cs`
files follow it. Comparable in spirit to one-handler-per-use-case, just on the presentation side instead of the
application side.

### 3.4 The CQRS spectrum

CQRS is a dial, not a binary. Pick the point that matches the service:

| Level              | Reads/writes                                                                   | What it adds                                               | Used here?                                                                                                                                        |
|--------------------|--------------------------------------------------------------------------------|------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------|
| **CQRS-lite**      | Separate request types + handlers, **one database**                            | Intent clarity, validation/audit pipeline, no storage cost | ✅ this course                                                                                                                                    |
| **Full CQRS**      | Separate **read model** (projections, `AsNoTracking` query DTOs, or a read DB) | Read optimization, denormalized views                      | partially — the finished project's `ArticleHub` service is a read-only aggregate view fed by **events**, which is the real thing at service scale |
| **Event Sourcing** | State = replayed event log                                                     | Full audit, time travel; big complexity                    | ❌ not here                                                                                                                                       |

The heavyweight version (separate stores, eventual consistency) is what "CQRS" often means in articles — the calibration
between the two is exactly why `NOTES.md` has the CQRS section.

### 3.5 Event-driven architecture (the cross-service half)

Within a service: MediatR domain events, dispatched on `SaveChanges` by an interceptor. Across services: **integration
events** over a message bus — MassTransit + RabbitMQ (`Articles.Integration.Contracts` in BuildingBlocks,
`PublishArticleApprovedEventHandler.cs`
publishing, `ArticleHub` consuming). Rule of thumb from the repo guardrails:

> Domain events = within a service boundary. Integration events = across services.

This is the piece that turns a set of services into a system: services stay unaware of each other's internals and react
to published facts instead.

### 3.6 Modular monolith vs. microservices

- **Modular monolith:** one deployable, hard module boundaries (`src/Articles/Modules/EmailService`,
  `FileService` are exactly this — pluggable, own dependencies, no HTTP of their own).
- **Microservices:** separately deployed services owning their data (`Services/Submission`,
  `Services/Auth`, …), talking via gRPC (`Articles.Grpc.Contracts`) and integration events.
- The repo's stated rule: **default to module; extract a microservice only when deployment, scaling or ownership demands
  it.** Starting modular-monolith and extracting later is cheaper than starting distributed and untangling network
  failure modes.

---

## 4. Choosing: a decision guide

Ask, in order:

1. **Is this a service boundary or a module?** Default module (`Modules/`-style). Microservice only for independent
   deploy/scale/ownership.
2. **How much domain logic?**
    - Little (CRUD, glue, aggregation) → **Endpoint → DTO → Service** is honest and fast; or Minimal API handlers
      straight onto repositories for trivial cases (the Journals service's Redis CRUD endpoints do this —
      `Journals.API/Features/Journals/Create/CreateJournalEndpoint.cs`).
    - Rich invariants and workflows → **CQRS-lite + rich domain** (the course pattern). Rules on aggregates, handlers as
      thin coordinators.
3. **Do reads need optimizing?** Start CQRS-lite. Add projections/read models (ArticleHub-style event-fed views) when a
   specific read proves hot.
4. **Same core, multiple transports?** REST + gRPC from one application core (Auth does this) — the Clean Architecture
   layering is what makes that nearly free.
5. **Cross-service communication?** Integration events over the bus for state changes; gRPC for synchronous queries;
   never share a database between services.

## 5. Pattern cheat sheet

| Pattern                            | One-line definition                                      | Where seen in this repo                                           |
|------------------------------------|----------------------------------------------------------|-------------------------------------------------------------------|
| Clean Architecture                 | Domain at center, dependencies point inward              | `Submission.{Api,Application,Domain,Persistence}`                 |
| Vertical slices                    | One folder per feature, all its code                     | `Submission.Application/Features/<Feature>/`                      |
| CQRS-lite                          | Separate command/query types + handlers, shared DB       | `CreateArticleCommand` vs (coming) `GetArticleQuery`              |
| Minimal API endpoint               | `MapPost(...)` static entry, thin                        | `Submission.Api/Endpoints/CreateArticleEndpoint.cs`               |
| REPR / FastEndpoints               | Class-per-endpoint: Request, Endpoint, Response          | `Auth.API/Features/Users/Login/LoginEndpoint.cs`                  |
| Service Layer / Transaction Script | Endpoint → DTO → `EntityService` with the rules          | Not used here — Auth endpoints are the closest cousin             |
| Hexagonal / Ports & Adapters       | Core exposes interfaces; adapters implement them         | Repository interfaces + EF/Redis implementations                  |
| Event-driven                       | Facts published, consumers react                         | `PublishArticleApprovedEventHandler` → MassTransit → `ArticleHub` |
| Modular monolith                   | Hard module boundaries, single deployable                | `Modules/EmailService`, `Modules/FileService`                     |
| Microservices                      | Independently deployed services, own data, gRPC + events | `Services/*`                                                      |
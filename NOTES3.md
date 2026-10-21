# NOTES3 — Scaffolding a new service from zero

A blueprint for starting a **new** .NET service: minimal APIs + vertical slices + FluentValidation +
DTOs + ProblemDetails, with a Domain that stays deletable.

This file is prescriptive ("do this"), unlike NOTES1/NOTES2 which explain existing code. Where the
course project already demonstrates a point, the file path is referenced. Where the course project
diverges from what I'd scaffold fresh, that's called out explicitly.

---

## 0. The stack I'm assuming

| Concern | Choice | Why |
|---|---|---|
| HTTP | **Minimal API** (`MapGet`/`MapPost`) | No controller ceremony; endpoint ≈ use case |
| Endpoint discovery | Carter (`ICarterModule`) *or* explicit `MapAllEndpoints()` | Both fine; see §7.3 |
| Request pipeline | **MediatR** (`ISender.Send`) | Gives one seam for validation/logging/identity behaviors |
| Validation | **FluentValidation** + pipeline behavior | Rules live next to the feature, not on the DTO |
| Contracts | `record` command/query + `record` response | Immutable, value equality, JSON-friendly |
| Errors | **`IExceptionHandler` + `AddProblemDetails()`** | Native, RFC 9457; replaces hand-rolled middleware |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` + Scalar | Built into .NET 9+; Swashbuckle no longer in templates |
| ORM | EF Core, `IEntityTypeConfiguration<T>` | Fluent-first (NOTES2:1089) |
| Layering | API → Application → Domain, Persistence → Domain | See §2 for the "deletable Domain" rule |

The two non-obvious picks are MediatR and the `IExceptionHandler` approach — both are justified below,
and both have a "skip it if…" escape hatch.

---

## 1. Project layout

```
src/Services/<Name>/
├── <Name>.Domain/          # entities, value objects, domain events, invariants
├── <Name>.Application/     # Features/<Slice>/ — commands, queries, handlers, validators, DTOs
├── <Name>.Persistence/     # DbContext, entity configurations, repositories, migrations
└── <Name>.API/             # Program.cs, endpoints, middleware, DI composition
```

**Reference direction (enforced, not aspirational):**

```
API ──▶ Application ──▶ Domain
 │                        ▲
 └──▶ Persistence ────────┘
```

- `Domain` references **nothing** but `Blocks.Domain` (+ `Articles.Abstractions` for shared enums).
- `Application` references `Domain`. Never `Persistence`.
- `Persistence` references `Domain`. Never `Application`.
- `API` references all three (it's the composition root — that's its job).

**The single most common violation to watch for:** a `Domain` project referencing gRPC contracts or
`FileStorage.Contracts`. The finished course project does exactly this —
`Services/Submission/Submission.Domain/Behaviors/Person.cs:1: using Auth.Grpc;` and
`Asset.cs:1: using FileStorage.Contracts;`. That means the domain model can't be understood, tested,
or moved without the transport layer. In a fresh scaffold, translate gRPC responses into domain
concepts **in the Application handler**, not inside the entity.

> **Insight:** "Domain has no dependencies" is not purity for its own sake. It's what makes the
> Domain project *testable without a test host* and *deletable without a rewrite* (§2).

---

## 2. Making the Domain deletable

This is the part worth designing for up front, because it's nearly impossible to retrofit.

### The rule

> **Nothing `public` in `Application/Features/**` may expose a `Domain` type.**

Commands, queries, responses, and DTOs use only: primitives, `Guid`/`int`, enums from
`Articles.Abstractions`, and DTOs. Domain entities appear **only inside handler bodies** — as local
variables, never in a signature.

```csharp
// ✅ Deletable-Domain-safe
public record GetArticleQuery(int ArticleId) : IQuery<GetArticleResponse>;
public record GetArticleResponse(ArticleDto Article);

public class GetArticleHandler(ArticleRepository repo)
    : IRequestHandler<GetArticleQuery, GetArticleResponse>
{
    public async Task<GetArticleResponse> Handle(GetArticleQuery query, CancellationToken ct)
    {
        var article = await repo.GetFullArticleByIdAsync(query.ArticleId, ct);  // Domain type — local only
        return new GetArticleResponse(article.Adapt<ArticleDto>());
    }
}

// ❌ Leaks the Domain into the contract
public record GetArticleResponse(Article Article);   // API now depends on the entity graph
```

### Why this works

If every public contract is Domain-free, then deleting `X.Domain` produces a **compile-error blast
radius confined to handler bodies**. The endpoints, routes, OpenAPI document, DTOs, and every
client consuming the API are untouched. You rewrite the inside of each `Handle` to use whatever
replaced the domain (anemic DTOs, raw SQL, a read model) and the service still satisfies the same
contract.

Contrast with the course project's finished `GetArticleEndpoint`, which is fine today but shows the
coupling risk: the response type `GetArticleResponse(ArticleDto)` is safe, but
`GetFullArticleByIdAsync` returns a **fully materialized aggregate** — so the handler's internals are
inseparable from the domain model. Deleting `Submission.Domain` would mean rewriting every handler
*and* every repository method.

### Three enforcement mechanisms (pick one, they stack)

1. **`internal` entities.** Put entities in `X.Domain` as `internal` and add
   `[assembly: InternalsVisibleTo("X.Application")]`. The compiler now enforces the rule for you.
   Downside: `internal` in a separate assembly is awkward for EF and for tests.
2. **Architecture tests.** `NetArchTest.Rules` or `ArchUnitNET` in a test project:
   ```csharp
   var result = Types.InAssembly(typeof(GetArticleQuery).Assembly)
       .That().HaveNameEndingWith("Response")
       .ShouldNot().HaveDependencyOn("X.Domain")
       .GetResult();
   ```
   This is the version I'd actually use — it catches the violation at build time and documents the
   intent in code.
3. **A folder convention.** Domain types only reachable via `Persistence/Repositories`, which return
   DTOs. Weakest option, but free.

> **Insight:** the deletability you get is proportional to how narrow your handler *signatures* are,
> not how small your Domain is. A 200-entity Domain with DTO-only contracts is more deletable than a
> 5-entity Domain whose entities are in every response.

### What "delete it later" actually means in practice

Realistic reasons to delete or replace a Domain layer:

- The service turns out to be a thin pass-through / aggregation layer (no invariants) — anemic
  entities were never earning their keep. **`ArticleHub` is exactly this**: it has
  `ArticleHub.Domain/Entities/` but is a read-only event-projection service. Its entities are
  persistence shapes, not a model.
- The logic moves to another service and this one becomes a proxy.
- You migrate to a read-model-only service backed by a projection store.

None of those should require touching the HTTP surface. That's the goal.

---

## 3. Vertical slices

One folder per use case, containing everything that use case needs.

```
<Name>.Application/
├── Features/
│   ├── _Shared/                      # cross-slice bases (ArticleCommand, shared validators)
│   ├── CreateArticle/
│   │   ├── CreateArticleCommand.cs          # request record + FluentValidation validator
│   │   ├── CreateArticleCommandHandler.cs   # orchestration
│   │   └── CreateArticleCommandValidator.cs # (or inline in Command.cs — see §5.3)
│   ├── GetArticle/
│   │   ├── GetArticleQuery.cs
│   │   ├── GetArticleQueryHandler.cs
│   │   └── ArticleDto.cs                    # slice-local DTO
│   └── ApproveArticle/
│       ├── ApproveArticleCommand.cs
│       ├── ApproveArticleCommandHandler.cs
│       └── ApproveArticleDomainEventHandler.cs   # reactions owned by this slice
```

**Rules:**

- A slice may not reference another slice's types, except through `_Shared`.
- DTOs live with the slice that produces them, unless two slices genuinely share one — then promote
  to `Dtos/` or `_Shared/`.
- Domain event handlers live in the slice that *cares*, not next to the event.
- New feature = new folder + zero DI edits (assembly scanning handles registration).

**Why vertical slices over horizontal layers:**

| | Horizontal (`Services/`, `Handlers/`, `Validators/`) | Vertical (`Features/CreateArticle/`) |
|---|---|---|
| Add a feature | Touch 4 folders | Add 1 folder |
| Delete a feature | Hunt for orphans across the tree | Delete 1 folder |
| Read a feature | Jump between 4 files in 4 places | Everything in one place |
| Merge conflicts | Everyone edits the same folders | Different slices don't collide |
| Cross-cutting change | Easy (one folder) | Needs behaviors/filters — see §5.4 |

The honest trade-off: horizontal layering makes *cross-cutting* changes cheap and *feature* changes
expensive. Vertical slicing inverts that. Since most work is feature work, vertical wins — **provided
you have the behavior/filter mechanism** for cross-cutting concerns. Without MediatR behaviors, you'd
reintroduce duplication across every slice.

### Slices vs. the "delete Domain" goal, combined

The strongest version: **the Domain project holds only what multiple slices share.** A concept used by
exactly one slice can live as a private type in that slice.

```
Features/ApproveArticle/
├── ApproveArticleCommand.cs
├── ApproveArticleCommandHandler.cs
└── _Domain/                          # only this slice uses these
    ├── ApprovalPolicy.cs
    └── ApprovalPolicyTests.cs
```

Deleting the feature deletes its domain rules with it. This is "modular monolith inside a
microservice" — a genuinely senior-level structure, and it's what makes the `X.Domain` project
optional rather than mandatory.

---

## 4. Contracts: commands, queries, DTOs

### 4.1 The command/query record

```csharp
public record CreateArticleCommand(int JournalId, string Title, ArticleType Type, string Scope)
    : ArticleCommand;
```

- `record` + positional params → immutable, value equality, trivial to test.
- **It doubles as the API request contract** (bound from the JSON body). This is a deliberate trade:
  one type instead of two + a mapper, at the cost of coupling the HTTP contract to the application
  contract. In a service-per-bounded-context architecture this is usually right; see §11.3.
- `[JsonIgnore]` on server-derived fields (`CreatedById`, `ArticleId` when it comes from the route)
  so clients can't spoof them. Course project: `Features/_Shared/ArticleCommand.cs`.

### 4.2 Response DTOs

```csharp
public record ArticleDto(
    int Id,
    string Title,
    string Status,
    IEnumerable<ActorDto> Actors
);
```

- `record` for responses, not classes. Value equality makes assertions clean.
- **Never return Domain entities.** Same rule as §2 — this is where it's most tempting to break.
- Map with Mapster (`.Adapt<T>()`) or explicit constructors. Explicit constructors are more code but
  survive refactoring better and show up in a diff.

### 4.3 Command vs query — what actually differs

| | Command | Query |
|---|---|---|
| Changes state | Yes | No |
| Loads aggregate | Yes, to enforce invariants | **No** — project straight to a DTO |
| Tracking | On | `AsNoTracking()` |
| Return | Id / `Unit` | A DTO shaped for the caller |
| Idempotent | Ideally not assumed | Yes |

**The query side is where most scaffolds waste their CQRS budget.** A query handler that loads a
full aggregate and maps it in memory is a write-shaped query. Do this instead:

```csharp
public class GetArticleHandler(AppDbContext db)
    : IRequestHandler<GetArticleQuery, GetArticleResponse>
{
    public async Task<GetArticleResponse> Handle(GetArticleQuery query, CancellationToken ct)
    {
        var dto = await db.Articles
            .AsNoTracking()
            .Where(a => a.Id == query.ArticleId)
            .ProjectToType<ArticleDto>()      // SELECT only the DTO's columns
            .FirstOrDefaultAsync(ct);

        return new GetArticleResponse(Guard.NotFound(dto));
    }
}
```

One round trip, no tracking, no aggregate graph. **This is the payoff that justifies separating
queries from commands at all** — if you never use it, the split is just naming.

### 4.4 Marker interfaces

```csharp
public interface ICommand : ICommand<Unit>;
public interface ICommand<out TResponse> : IRequest<TResponse>;
public interface IQuery<out TResponse> : IRequest<TResponse> where TResponse : notnull;
```

Thin naming layer over MediatR — no new mechanism, but it makes a signature self-describing and lets
behaviors target only one side (e.g. a read-only caching behavior for `IQuery<>`). The course project
adds these in the finished version (`BuildingBlocks/Blocks.MediatR/Abstractions/`).

---

## 5. Validation

### 5.1 FluentValidation + pipeline behavior (recommended with MediatR)

```csharp
public class CreateArticleCommandValidator : AbstractValidator<CreateArticleCommand>
{
    public CreateArticleCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(MaxLength.C256);
        RuleFor(x => x.JournalId).GreaterThan(0);
    }
}
```

```csharp
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

Registered once, applies to every handler:
```csharp
services.AddValidatorsFromAssemblyContaining<CreateArticleCommandValidator>();
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(SetUserIdBehavior<,>));
});
```

Course project equivalent: `Submission.Application/DependencyInjection.cs:20-30` and
`Blocks.MediatR/Behaviors/ValidationBehavior.cs`.

**Why throw instead of returning a result?** Because it gives validation exactly one exit point, and
the exception handler (§6) converts it to a 400 ProblemDetails. The alternative — every handler
returning `Result<T>` — spreads error handling through every signature.

### 5.2 FluentValidation + endpoint filter (no MediatR)

```csharp
public sealed class ValidationFilter<TRequest>(IValidator<TRequest>? validator = null) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx,
        EndpointFilterDelegate next)
    {
        var request = ctx.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null || validator is null) return await next(ctx);

        var result = await validator.ValidateAsync(request);
        if (!result.IsValid)
            return TypedResults.ValidationProblem(result.ToDictionary());

        return await next(ctx);
    }
}
```

```csharp
app.MapPost("/api/articles", CreateArticle)
   .AddEndpointFilter<ValidationFilter<CreateArticleCommand>>();
```

`AddEndpointFilter<T>` needs a **closed** generic. For automatic application, use
`AddEndpointFilterFactory`:

```csharp
public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder) =>
    builder.AddEndpointFilterFactory((routeContext, next) =>
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(typeof(TRequest));
        return async ctx =>
        {
            var validator = ctx.HttpContext.RequestServices.GetService(validatorType) as IValidator<TRequest>;
            if (validator is not null)
            {
                var request = ctx.Arguments.OfType<TRequest>().FirstOrDefault();
                if (request is not null)
                {
                    var result = await validator.ValidateAsync(request);
                    if (!result.IsValid) return TypedResults.ValidationProblem(result.ToDictionary());
                }
            }
            return await next(ctx);
        };
    });
```

### 5.3 .NET 10 built-in validation — the native alternative

Verified present in the local `Microsoft.AspNetCore.App` 10.0.2 shared framework
(`Microsoft.Extensions.Validation.dll`):

- `AddValidation()` on `IServiceCollection` — `Microsoft.Extensions.DependencyInjection.ValidationServiceCollectionExtensions`
- `ValidationOptions.Resolvers` — an ordered list of `IValidatableInfoResolver`
- `ValidationOptions.MaxDepth` — guards against circular reference blowups
- `[ValidatableType]` — opt a type in explicitly
- `[SkipValidation]` — opt a type/parameter out
- `IValidatableInfoResolver.TryGetValidatableTypeInfo(...)` / `TryGetValidatableParameterInfo(...)` — the extension point

With it, DataAnnotations on the request type are validated automatically for minimal API parameters,
and failures become a validation ProblemDetails. **No filter, no behavior, no package.**

```csharp
builder.Services.AddValidation();

public record CreateArticleRequest(
    [Required, MaxLength(256)] string Title,
    [Range(1, int.MaxValue)] int JournalId);
```

The catch: it's **DataAnnotations-only out of the box**. To use FluentValidation you'd write an
`IValidatableInfoResolver` that looks up `IValidator<T>` from DI and adapts it to
`IValidatableInfo` — which is more work than the filter in §5.2 and less documented. It's a genuine
option, not yet a replacement for FluentValidation.

> **Decision:** FluentValidation if rules are non-trivial or you already have MediatR; built-in
> `AddValidation()` if you want zero dependencies and DataAnnotations suffice. Don't run both.

### 5.4 Where each kind of validation belongs

| Kind | Where | Example |
|---|---|---|
| Shape / presence / range | FluentValidation on the command | `Title` not empty, `JournalId > 0` |
| Cross-field on the request | FluentValidation | `StartDate < EndDate` |
| Authorization | Endpoint (`.RequireAuthorization`) or `IAuthorizationHandler` | role checks |
| **Business invariants** | **Domain methods** — never a validator | "an article must have ≥1 author" |
| Uniqueness / existence | Handler (needs DB) | "journal name already taken" |

That fourth row is the one people get wrong. A validator that queries the database is a rule enforced
in two places (validator + domain) and guaranteed to drift. Course project's
`CreateJournalEndpoint.cs:24-27` does the duplicate-name check inline in the endpoint — acceptable
there because there's no domain model to protect, but in a real service that's a handler concern.

---

## 6. Errors → ProblemDetails

### 6.1 What to build

The finished course project hand-rolls `GlobalExceptionMiddleware`
(`Blocks.AspNetCore/Middlewares/GlobalExceptionMiddleware.cs`) which serializes an anonymous object.
It works, but it predates the native API and produces a **non-standard** body — not RFC 9457.

For a fresh scaffold, use `IExceptionHandler`:

```csharp
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            ValidationException        => (StatusCodes.Status400BadRequest, "Validation failed"),
            NotFoundException          => (StatusCodes.Status404NotFound, "Resource not found"),
            BadRequestException        => (StatusCodes.Status400BadRequest, "Bad request"),
            UnauthorizedException      => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ForbiddenException         => (StatusCodes.Status403Forbidden, "Forbidden"),
            DomainException            => (StatusCodes.Status422UnprocessableEntity, "Business rule violated"),
            OperationCanceledException => (499, "Client closed request"),
            _                          => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status >= 500)
            logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", http.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Status = status,
            Title  = title,
            Detail = env.IsDevelopment() ? ex.ToString() : null,
            Instance = http.Request.Path
        };
        problem.Extensions["traceId"] = http.TraceIdentifier;

        if (ex is ValidationException ve)
        {
            var errors = ve.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            problem = new HttpValidationProblemDetails(errors)
            {
                Status = status, Title = title, Instance = http.Request.Path
            };
            problem.Extensions["traceId"] = http.TraceIdentifier;
        }

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(problem, ct);
        return true;   // handled — stop the pipeline
    }
}
```

Registration:
```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// ...
app.UseExceptionHandler();     // NOT UseMiddleware<...>() — the native one
```

### 6.2 Why this over the hand-rolled middleware

| | Custom middleware | `IExceptionHandler` + `AddProblemDetails()` |
|---|---|---|
| Body shape | Whatever you serialize | RFC 9457 `application/problem+json` |
| Multiple handlers | One `switch` | Chain of `IExceptionHandler`, first `true` wins |
| Integration with `Results.Problem()` | None | Same `IProblemDetailsService` |
| Validation problem | Manual | `HttpValidationProblemDetails` built in |
| Testing | Spin up middleware | Handler is a plain class, unit-testable |
| Client expectations | Custom | Swagger/OpenAPI/HttpClient all understand it |

Both APIs verified present in .NET 10.0.2: `AddProblemDetails`, `IProblemDetailsService`,
`HttpValidationProblemDetails`, `IExceptionHandler.TryHandleAsync`.

### 6.3 The exception hierarchy

```
Exception
├── DomainException                    (Blocks.Domain)      → 422, business rule
├── HttpException                      (Blocks.Exceptions)  → mapped by its HttpStatusCode
│   ├── BadRequestException                                 → 400
│   ├── NotFoundException                                   → 404
│   └── UnauthorizedException                               → 401   ← finished project only
└── ValidationException                (FluentValidation)   → 400 + errors dict
```

**What actually exists today** — check before writing the handler:

| Type | Your `src/Articles` | Finished project |
|---|---|---|
| `HttpException` | ✅ | ✅ |
| `BadRequestException` | ✅ | ✅ |
| `NotFoundException` | ✅ | ✅ |
| `UnauthorizedException` | ❌ | ✅ |
| `DomainException` | ✅ (`Blocks.Domain/DomainException.cs`) | ✅ |
| `ForbiddenException` | ❌ | ❌ |

`NotFoundException` derives from `HttpException`, **not** from `DomainException` — so the
`switch` order above matters: match the concrete types, and put `HttpException` as a catch-all
before the default arm if you want any unmapped subclass to still produce its own status code:

```csharp
HttpException http => (http.HttpStatusCode, http.Message),   // covers subclasses not listed
```

Note the finished project's `GlobalExceptionMiddleware` does *not* do this — it switches on
`NotFoundException`/`BadRequestException` individually, so any other `HttpException` subclass
(including the `UnauthorizedException` it defines!) falls through to **500**. That's a real bug
worth not copying.

Rules:
- **Throw `DomainException` for invariant violations** — it's the domain's own vocabulary, and it
  doesn't reference HTTP. Mapping to a status code is the API layer's job (§1 dependency direction).
- `HttpException` subclasses are a pragmatic shortcut when the application layer already knows the
  status. They do couple you to HTTP semantics, so use them in handlers, not in entities.
- **Never throw for expected control flow.** "Not found" is a legitimate 404, but "user typed a bad
  id" is a 400/404 — both fine. What's not fine is exceptions for branching.
- 499 for client-disconnect: non-standard (nginx) but widely understood, and it keeps your 5xx
  dashboard clean of client aborts. Course project already does this
  (`GlobalExceptionMiddleware.cs:39-43`).

### 6.4 Declaring responses in OpenAPI

With `TypedResults` return types, OpenAPI infers most of this. Be explicit where it matters:

```csharp
.Produces<CreateArticleResponse>(StatusCodes.Status201Created)
.ProducesValidationProblem(StatusCodes.Status400BadRequest)
.ProducesProblem(StatusCodes.Status401Unauthorized)
.ProducesProblem(StatusCodes.Status404NotFound)
```

`TypedResults` over `Results`: `Results.Ok(x)` returns `IResult`, so OpenAPI can't infer the body
type and you *must* add `.Produces<T>()`. `TypedResults.Ok(x)` returns `Ok<T>` and the schema is
inferred. Prefer `TypedResults`.

---

## 7. Endpoints (minimal API)

### 7.1 Shape

```csharp
public static class CreateArticleEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/articles", async (
                CreateArticleCommand command,
                ISender sender,
                CancellationToken ct) =>
            {
                var response = await sender.Send(command, ct);
                return TypedResults.Created($"/api/articles/{response.Id}", response);
            })
            .RequireAuthorization(policy => policy.RequireRole(Role.AUT))
            .WithName("CreateArticle")
            .WithTags("Articles")
            .WithSummary("Create a new article")
            .Produces<CreateArticleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
```

Non-negotiables:
- **Endpoints are thin.** Bind → send → map result. If there's logic here, it belongs in the handler
  (§11.1).
- **Always take `CancellationToken ct`** and forward it. Course project's
  `CreateArticleEndpoint.cs` omits it; NOTES1:1721 already flags this as the cheapest real
  improvement available.
- **`TypedResults`, not `Results`.**
- **`MapGroup("/api")` once**, then all routes are relative. Course project's
  `Submission.Api/Endpoints/EndpointRegistration.cs` writes `"api/articles"` per-route, so the prefix
  is duplicated across every endpoint and can drift.

### 7.2 Route conventions

| Verb | Route | Returns |
|---|---|---|
| `GET /articles/{id:int}` | fetch one | `200` + DTO, `404` |
| `GET /articles?page=&size=` | search | `200` + paged DTO |
| `POST /articles` | create | `201` + `Location` header |
| `PUT /articles/{id}` | full replace | `204` / `200` |
| `PATCH /articles/{id}` | partial | `204` / `200` |
| `DELETE /articles/{id}` | remove | `204` |

Use `:int` / `:guid` route constraints — they give you a 404 for free instead of a model-binding 400.

### 7.3 Endpoint discovery — three options

| Option | Registration | Files | Notes |
|---|---|---|---|
| Bare minimal API | manual `MapAllEndpoints()` | `Program.cs` + 1 registration file | Honest up to ~10 endpoints |
| **Carter** | `.AddCarter()` / `MapCarter()` | 1 class per module | `ICarterModule.AddRoutes(app)`; code inside is plain minimal API |
| FastEndpoints | `.AddFastEndpoints()` / `UseFastEndpoints()` | 1 class per endpoint | `Endpoint<TReq,TRes>`; brings its own binding + `Validator<T>` |

Carter is **not** an alternative to minimal API — it's a discovery layer over it. You write the same
`MapGet`, and it scans for `ICarterModule` so nobody has to call it.

For a fresh scaffold I'd use **Carter**: one concept, zero lock-in, and the code inside a module is
portable minimal API. The manual registration file is exactly the thing Carter deletes — see the
finished project's `Submission.API/Endpoints/XEndpointRegistration.cs`, ten hand-maintained lines.

**Prefix subtlety:** `app.MapGroup("/api").MapCarter()` passes the group to every module, so a
module's `MapGet("/articles/{id:int}")` becomes `/api/articles/{id}`. With FastEndpoints the route
attribute is absolute and the prefix comes from config. Moving an endpoint between the two silently
changes the URL.

---

## 8. Composition root

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiServices(builder.Configuration)          // HTTP, auth, OpenAPI, filters
    .AddApplicationServices(builder.Configuration)  // MediatR, validators, mapster
    .AddPersistenceServices(builder.Configuration); // DbContext, repositories

var app = builder.Build();

app.UseExceptionHandler();                          // first — catches everything below
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api").MapCarter();                   // or MapAllEndpoints()
app.MapHealthChecks("/health");

app.Run();
```

**Ordering that matters:**

1. `UseExceptionHandler()` **first** so it wraps everything.
2. `UseAuthentication()` **before** `UseAuthorization()` — always.
3. `UseRouting()` is implicit in minimal APIs; you don't need to call it unless you have a specific
   reason. The course project calls it (`Review.API/Program.cs:40`) — harmless but redundant.
4. `UseCors()` before auth if used.

One DI extension method per layer, each taking `IConfiguration`:

```csharp
public static IServiceCollection AddApplicationServices(
    this IServiceCollection services, IConfiguration configuration)
{
    services
        .AddValidatorsFromAssemblyContaining<CreateArticleCommandValidator>()
        .AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });
    return services;
}
```

This is the one place that knows the full object graph. Keep it boring.

---

## 9. Cross-cutting checklist

Things that are painful to add later and cheap to add now:

| Concern | Add | Notes |
|---|---|---|
| **Options validation** | `.AddAndValidateOptions<T>(config)` | `Bind` → `ValidateDataAnnotations` → `ValidateOnStart`. NOTES2:80-166 |
| **Correlation/trace id** | `http.TraceIdentifier` in ProblemDetails | Free; put it in every error body |
| **Structured logging** | Serilog + `UseSerilogRequestLogging()` | One line per request with status + duration |
| **OpenAPI** | `AddOpenApi()` + `MapOpenApi()` | .NET 9+ built-in; `Microsoft.AspNetCore.OpenApi` package |
| **API docs UI** | Scalar (`Scalar.AspNetCore`) | `app.MapScalarApiReference()` — the modern Swagger UI |
| **Health checks** | `AddHealthChecks().AddDbContextCheck<T>()` | `MapHealthChecks("/health")`; required for k8s probes |
| **CancellationToken** | Every endpoint + handler + repository | §7.1 |
| **Global usings** | `GlobalUsing.cs` per project | NOTES1:120 |
| **Central package mgmt** | `Directory.Packages.props` | One version per package, repo-wide |
| **Shared build props** | `Directory.Build.props` | TFM, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors` |
| **SDK pin** | `global.json` | Stops "works on my machine" |
| **OpenAPI security scheme** | JWT bearer in `AddOpenApi` | Otherwise you can't test auth'd endpoints from the UI |
| **Rate limiting** | `AddRateLimiter` + `RequireRateLimiting` | .NET 7+ built-in, no package |
| **Output caching** | `AddOutputCache` + `.CacheOutput()` | Only for genuinely cacheable GETs |

**`Directory.Build.props`** at the repo root:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

**`Directory.Packages.props`** (Central Package Management):
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="FluentValidation.DependencyInjectionExtensions" Version="12.0.0" />
    <PackageVersion Include="MediatR" Version="12.4.1" />
    <PackageVersion Include="Carter" Version="10.0.0" />
  </ItemGroup>
</Project>
```
Then each `.csproj` has `<PackageReference Include="MediatR" />` with **no version** — one place to
bump. The course project does *not* do this; every csproj carries its own versions, which is why you
see `Carter 10.0.0` in two places and a stray `Swashbuckle.AspNetCore 10.2.3` in
`Submission.Api.csproj` that nothing uses.

---

## 10. Scaffolding order (copy this)

1. **Solution + `Directory.Build.props` + `Directory.Packages.props` + `global.json` + `.editorconfig`.**
2. **Four projects**, references wired per §1. Verify `Domain` has **zero** package references.
3. **`Domain`**: one entity, one value object, one domain event. `Blocks.Domain` base classes only.
4. **`Persistence`**: `DbContext` with `ApplyConfigurationsFromAssembly`, one
   `IEntityTypeConfiguration<T>`, design-time factory, first migration.
5. **`Application`**: `AddApplicationServices`, `ValidationBehavior`, one command + handler +
   validator, one query + handler + DTO.
6. **`API`**: `AddApiServices`, `GlobalExceptionHandler`, `Program.cs`, one endpoint. Wire
   `AddValidation`/filter, `AddProblemDetails`, `AddOpenApi`.
7. **Run it.** Confirm: happy path, 400 with an errors dictionary, 404, and a 500 that leaks no
   stack trace in Production.
8. **Tests**: `WebApplicationFactory` + one happy-path and one failure-path integration test,
   `Testcontainers` for the DB. Add the architecture test from §2.
9. **Only then** add the second feature — and confirm it required **zero DI edits**.

Step 9 is the acceptance test for the whole structure.

---

## 11. Anti-patterns to avoid

### 11.1 Fat endpoints (Transactional Script in CQRS clothing)

The course project's `Journals.API/Features/Journals/Create/CreateJournalEndpoint.cs:20-51` does
duplicate checking, gRPC calls, entity construction, repository writes, and event publishing — all
inline. `CreateJournalCommand` is an empty record with no handler.

That's not CQRS; it's a transaction script. It's *acceptable* for trivial CRUD with no invariants,
but it's the shape that metastasizes. If an endpoint has more than bind→send→map, move it.

### 11.2 Interfaces for everything

```csharp
public interface IArticleService { Task<Article> GetAsync(int id); }
```

Don't. Guardrails already say this: no service-layer classes, no interfaces without multiple
implementations. Handlers, repositories, and domain methods cover it.

### 11.3 Merging the API contract with the command (know the cost)

Fine in a service-per-bounded-context architecture; a problem when the API is public or versioned.
The tell that you've outgrown it: you need a field in the API that the command shouldn't have (or
vice versa), and you start adding `[JsonIgnore]` to compensate.

### 11.4 Validators that hit the database

See §5.4. A rule split between validator and domain is a rule enforced nowhere.

### 11.5 Returning entities from endpoints

Breaks §2, leaks your schema, and creates accidental API contracts — rename a domain property and
you've made a breaking API change.

### 11.6 Anemic Domain + rich handlers

If entities are bags of properties and all logic is in handlers, you have the cost of DDD with none
of the benefit. Either put invariants on the aggregate (`journal.CreateArticle(...)`) or drop the
Domain project (§2). `ArticleHub` is the honest example of choosing the latter.

### 11.7 Custom error bodies

Hand-rolling `{ statusCode, message, traceId }` (as the course project's middleware does) means every
client writes custom parsing. RFC 9457 exists so nobody has to.

---

## 12. What I'd change in the course project

Ordered by value, not effort:

1. **Add `CancellationToken` end-to-end.** NOTES1:1721 already has the plan.
2. **Replace `GlobalExceptionMiddleware` with `IExceptionHandler` + `AddProblemDetails()`.** §6.
3. **`MapGroup("/api")` once** instead of `"api/..."` on every route.
4. **`TypedResults` instead of `Results`** so OpenAPI infers schemas.
5. **Use the read-side seam**: `AsNoTracking()` + `ProjectToType` in query handlers. Right now
   queries materialize full aggregates — the CQRS split isn't paying for itself.
6. **Central Package Management.** Remove the unused `Swashbuckle` reference from
   `Submission.Api.csproj`.
7. **Stop `Domain` from referencing gRPC/FileStorage contracts** (§1).
8. **Architecture test** asserting responses don't depend on `*.Domain` (§2).
9. **Move `CreateJournalEndpoint` logic into a handler** — or accept it as a documented exception.

---

## Quick reference: new feature, start to finish

```
1. Application/Features/<Slice>/<Slice>Command.cs        — record + validator
2. Application/Features/<Slice>/<Slice>CommandHandler.cs — IRequestHandler
3. Application/Features/<Slice>/<Slice>Response.cs       — DTO (if it returns data)
4. API/Endpoints/<Slice>Endpoint.cs                      — Map → Send → TypedResults
5. Domain/<Entity>.cs                                    — invariant method (if needed)
6. Persistence/EntityConfigurations/<Entity>Config.cs     — only if schema changed
7. Tests/Features/<Slice>Tests.cs
```

Zero DI edits. Zero registration edits. If any of those are needed, the structure is wrong.

# LEARN — topics to revisit

Topics I didn't fully understand. Come back to these later.

---

## C# extension methods & the `this` keyword

**File:** `src/Articles/BuildingBlocks/Blocks.EntityFrameworkCore/MediatorExtensions.cs`
**Date added:** 2026-10-10

### The confusion I had
- I thought `this` on the first parameter means DI can inject the parameter without instantiating it.
- That's wrong — `this` has nothing to do with dependency injection.

### What to re-learn
- [ ] **Extension method basics**: `this IMediator mediator` in a `static class` is syntax sugar —
      `mediator.DispatchDomainEventsAsync(ctx)` compiles to `MediatorExtensions.DispatchDomainEventsAsync(mediator, ctx)`.
- [ ] **Why the class must be `static`**: extension methods are always called statically, so the class is never
      instantiated — a static class enforces that (no `new`, no instance members, no inheritance).
- [ ] **Rules for extension methods**: static class, top-level (not nested), non-generic; method is `static`
      with first parameter marked `this`.
- [ ] **Extension methods ≠ DI**: two independent mechanisms.
      - DI container: creates the `IMediator` instance and injects it (e.g. into an endpoint's constructor).
      - `this` extension: only changes how the *call* looks — the object must already exist.
- [ ] **Resolution**: compiler looks at `using` directives for static classes with matching `this`-parameter types —
      namespace matters more than the class name. Same pattern as LINQ's `Enumerable` class (`Where`, `Select`, ...).
- [ ] **Mental model**: extension method = sticky note attached to someone else's type. You can't add methods to
      `IMediator` (library type), but a static class lets you "stick" your method onto it.

### Questions to test myself later
- Can an extension method be called on a `null` receiver? (Yes — it's just a static call with the first arg null;
  a NullReferenceException only happens if the method body dereferences it.)
- Why can't extension methods access private members of the type they extend?
- Where does the DI container actually instantiate `IMediator`, and how does it reach my handler?

---

## Dispatching domain events from the EF ChangeTracker

**File:** `src/Articles/BuildingBlocks/Blocks.EntityFrameworkCore/MediatorExtensions.cs`
**Related:** `Blocks.Domain/Entities/AggregateEntity.cs`, `Interceptors/DispatchDomainEventsInterceptor.cs` (stubs so far)
**Date added:** 2026-10-10

### The code I want to understand

```csharp
public static async Task<int> DispatchDomainEventsAsync(this IMediator mediator, DbContext dbContext)
{
    var domainAggregates = dbContext.ChangeTracker.Entries<IAggregateEntity>();
    var domainEvents = domainAggregates.SelectMany(x => x.Entity.DomainEvents).ToList();
    domainAggregates.ToList().ForEach(x => x.Entity.ClearDomainEvents());

    foreach (var domainEvent in domainEvents)
    {
        await mediator.Publish(domainEvent);
    }

    return domainEvents.Count;
}
```

### What I want to re-learn
- [ ] **What the ChangeTracker is**: EF Core tracks every entity it loads/saves for the current `DbContext` instance.
      It knows each entity's state (`Added`, `Modified`, `Unchanged`, `Deleted`). Here we use it as a registry:
      "give me everything this context is currently watching."
- [ ] **`ChangeTracker.Entries<IAggregateEntity>()`**: filters tracked entities to only those implementing
      `IAggregateEntity` (our aggregate-root interface: `IEntity` + `DomainEvents` + `ClearDomainEvents()`).
      Returns `IEnumerable<EntityEntry<IAggregateEntity>>` — entries wrap the entity plus EF's tracking metadata.
- [ ] **Why aggregates only?** Domain events live on aggregate roots (`AggregateEntity._domainEvents` list).
      Child entities don't raise events; the root is the entry point for everything that happened in the aggregate.
- [ ] **Why `.ToList()` twice**: `SelectMany(...).ToList()` snapshots the events into a local list;
      then we **clear the events on the aggregates before publishing** so handlers that call `SaveChanges`
      (or re-enter this method) can't cause double-publishing / infinite loops. Order matters: snapshot → clear → publish.
- [ ] **`await mediator.Publish(event)` inside `foreach`**: publishes each event **sequentially, in order** —
      MediatR `Publish` notifies *all* handlers of *one* event before moving to the next. Worth contrasting with
      `Task.WhenAll` (parallel) and with MassTransit's `Publish` (integration events, cross-service, via RabbitMQ).
- [ ] **Return `domainEvents.Count`**: mostly for logging/debug — how many events fired during this save.
- [ ] **The interceptor connection**: `DispatchDomainEventsInterceptor : SaveChangesInterceptor` has empty overrides
      for now — the idea is `SavingChanges`/`SavedChanges` hooks call `DispatchDomainEventsAsync`.
      To learn: difference between dispatching **before** save (handlers see pre-save state, risk: handler side
      effects lost if save fails) vs **after** save (data is definitely persisted, risk: handler fails → save already
      committed, need outbox). Also how the interceptor gets registered (`AddDbContext(... options.UseInterceptors ...)`).
- [ ] **Full flow to be able to draw**: domain method raises event (`AddDomainEvent`) → handler/endpoint calls
      `SaveChangesAsync` → interceptor fires → `DispatchDomainEventsAsync` pulls events from ChangeTracker →
      MediatR publishes → per-event handlers run (same process, same DB transaction).

### Questions to test myself later
- What happens if a domain event handler itself calls `SaveChangesAsync`? Which side of the save do we dispatch on,
  and why does clearing events *before* publishing matter here?
- What's the difference between a **domain event** (MediatR, in-process) and an **integration event**
  (MassTransit/RabbitMQ, cross-service) in this project's architecture?
- If the process crashes after `SavedChanges` but before publishing, what state are we in? What pattern fixes this? (outbox)
- What is `EntityEntry<T>` and what else can it tell us besides the entity itself?

---

## The read side of CQRS — `AsNoTracking` + projection

**File:** `src/Articles/BuildingBlocks/Blocks.EntityFrameworkCore/CachedRepository.cs:14` (the only
place in the project that uses `AsNoTracking()`)
**Related:** NOTES3.md §4.3
**Date added:** 2026-10-21

### The confusion I had
- I thought separating queries from commands was itself the CQRS benefit.
- It isn't. The separation only *permits* the benefit. If a query handler still loads a full tracked
  aggregate and maps it in memory, the split bought me nothing but extra files.

### What to re-learn
- [ ] **What change tracking costs**: EF keeps a snapshot of every loaded entity to detect
      modifications. That's memory + CPU + a slower materialization path — all wasted on a read.
- [ ] **`AsNoTracking()`**: skip the snapshot. Safe for reads; the entity is detached, so mutating it
      and calling `SaveChanges` does nothing.
- [ ] **`AsNoTrackingWithIdentityResolution()`**: the variant that still de-duplicates entities within
      one query result (without it, the same author appearing twice becomes two objects).
- [ ] **`ProjectToType<TDto>()` (Mapster) / `Select(...)`**: translates the mapping into SQL, so only
      the DTO's columns are selected. This is the actual win — not "no tracking" but "no aggregate".
- [ ] **Why projection beats load-then-map**: `GetFullArticleByIdAsync` + `Adapt<ArticleDto>()` does
      N joins for the whole graph, then throws 90% away in memory. `ProjectToType` does the same joins
      but returns only the needed columns, and does it in the DB.
- [ ] **The trap**: `ProjectToType` needs Mapster to translate your config into SQL. Custom
      `MapWith`/`AfterMapping` logic silently falls back to in-memory mapping (or throws) — the
      projection must be expressible as SQL.
- [ ] **Split-query vs single-query** (`AsSplitQuery()`): when projecting a big graph, one giant JOIN
      can be worse than several smaller queries. Worth measuring, not guessing.

### Questions to test myself later
- If I `AsNoTracking()` an entity, modify it, and call `SaveChangesAsync`, what happens — exception,
  silent no-op, or write? Why?
- What does `ProjectToType` do with a navigation property that has a `MapWith` custom expression?
- How would I verify the generated SQL for a query handler, and where would I look for the N+1?

---

## `IExceptionHandler` and RFC 9457 ProblemDetails

**File (current, hand-rolled):** `finished/src/BuildingBlocks/Blocks.AspNetCore/Middlewares/GlobalExceptionMiddleware.cs`
**Related:** NOTES3.md §6
**Date added:** 2026-10-21

### The confusion I had
- I assumed `GlobalExceptionMiddleware` was *the* way to do this — it's what the course builds.
- It works, but it serializes an anonymous object. That body is not RFC 9457, so clients can't use
  standard ProblemDetails parsing, and `Results.Problem()` elsewhere in the app produces a
  *different* shape from the same API.

### What to re-learn
- [ ] **RFC 9457** (obsoletes RFC 7807): the standard fields — `type`, `title`, `status`, `detail`,
      `instance` — and `application/problem+json` as the content type.
- [ ] **`AddProblemDetails()`**: registers `IProblemDetailsService` + a default writer. After this,
      `Results.Problem()`, `TypedResults.ValidationProblem()`, and `IExceptionHandler` all produce the
      *same* body.
- [ ] **`IExceptionHandler.TryHandleAsync`**: return `true` to say "I handled it, stop"; `false` to pass
      to the next handler in the chain. That chaining is what the single `switch` in the custom
      middleware can't do — you can split handlers by concern (validation vs. domain vs. fallback).
- [ ] **`app.UseExceptionHandler()` vs `app.UseMiddleware<GlobalExceptionMiddleware>()`**: the native
      one is what makes the ProblemDetails services participate. Mixing them means two different bodies.
- [ ] **`HttpValidationProblemDetails`**: the subclass carrying the `errors` dictionary. Built in —
      no need to hand-roll `{ errors: [{ propertyName, errorMessage }] }`.
- [ ] **`ProblemDetailsContext.Exception` / `.AdditionalMetadata`**: how to enrich a problem from
      outside the handler (e.g. attaching a trace id via `AddProblemDetails(options => ...)`).
- [ ] **`IProblemDetailsWriter`**: the extension point for changing *how* a problem is written
      (e.g. to a legacy format for one old client).

### Questions to test myself later
- With two `IExceptionHandler` registrations, which runs first, and how does `false` change that?
- How do I get a stack trace into `detail` in Development but not Production, without leaking it via
  the exception message itself?
- Does `UseExceptionHandler()` still work if I call `UseMiddleware<GlobalExceptionMiddleware>()` before
  it? What body does the client actually get?
- What's the `ProblemDetails` equivalent for a 401 raised by the auth middleware (which never reaches
  my handler)?

---

## Endpoint filters and the open-generic problem

**File:** none yet in this project
**Related:** NOTES3.md §5.2
**Date added:** 2026-10-21

### The confusion I had
- I assumed `AddEndpointFilter<T>()` would work like `AddOpenBehavior(typeof(ValidationBehavior<,>))` —
  pass the open generic and let the container close it per endpoint.
- It doesn't. `AddEndpointFilter<TFilter>()` needs a **closed** type; it takes a concrete filter class.

### What to re-learn
- [ ] **Why the open generic doesn't work**: MediatR resolves behaviors from DI per request type, so
      `ValidationBehavior<,>` can be closed at resolution time. An endpoint filter is attached to a
      *route* at startup, not resolved per request.
- [ ] **`AddEndpointFilterFactory`**: gets `(routeContext, next)` and returns the actual filter delegate.
      This is where you can close the generic yourself and pull the right `IValidator<TRequest>` from
      `ctx.HttpContext.RequestServices`.
- [ ] **`EndpointFilterInvocationContext.Arguments`**: the bound arguments (body, route values, injected
      services). Note it's a grab-bag — `OfType<TRequest>()` is how you find your request object.
- [ ] **Returning short-circuits**: returning a value from the filter (e.g. `TypedResults.ValidationProblem`)
      skips the handler. Returning `await next(ctx)` continues.
- [ ] **`IEndpointFilter` vs middleware**: middleware is per-app/per-branch and has no idea what the
      endpoint's parameters are; a filter sees the bound arguments. That's the whole reason filters exist.

### Questions to test myself later
- Can I read route values (not just the body) inside an endpoint filter? What about DI services?
- How do I apply one filter to a whole `MapGroup` instead of endpoint by endpoint?
- What's the ordering guarantee between multiple filters on the same endpoint, and between filters and
  `RequireAuthorization`?

---

## .NET 10 built-in validation — `AddValidation()` and `IValidatableInfoResolver`

**File:** `Microsoft.Extensions.Validation.dll` in the `Microsoft.AspNetCore.App` 10.0.2 shared framework
**Related:** NOTES3.md §5.3
**Date added:** 2026-10-21

### The confusion I had
- I didn't know minimal APIs gained a first-class validation feature in .NET 10, or how it relates to
  FluentValidation. It's a *parallel* mechanism, not an integration.

### What to re-learn
- [ ] **`AddValidation()`** — the whole feature. Types are validated from DataAnnotations with no
      filter and no behavior; failures become a validation ProblemDetails.
- [ ] **`ValidationOptions.Resolvers`** — an ordered list of `IValidatableInfoResolver`; first one that
      returns a `IValidatableInfo` wins. This is how you plug in a different validation source.
- [ ] **`[ValidatableType]` vs. opt-in**: by default only types used as minimal API parameters are
      validated (to keep startup fast). `[ValidatableType]` forces a type to be included — needed for
      types reached only through nesting.
- [ ] **`[SkipValidation]`** — opting a type or parameter out.
- [ ] **`ValidationOptions.MaxDepth`** — guards circular references and deep graphs. Know the default
      and what happens when it's exceeded.
- [ ] **Writing an `IValidatableInfoResolver` that adapts FluentValidation**: `TryGetValidatableTypeInfo`
      and `TryGetValidatableParameterInfo` return a `ValidatableTypeInfo`/`ValidatableParameterInfo`,
      whose `ValidateAsync` you implement by delegating to `IValidator<T>` from DI. Then you get
      FluentValidation rules *and* the native pipeline. Non-trivial — this is the thing I'd want to
      actually build once to understand.
- [ ] **What it does NOT do**: no async DB-backed rules by default, no cross-request state, no
      replacement for domain invariants (NOTES3 §5.4).

### Questions to test myself later
- If I register both `AddValidation()` and a FluentValidation `ValidationBehavior`, which wins, and do
  I get two error responses?
- Does built-in validation run before or after model binding fails? What body does a binding failure
  produce with `AddProblemDetails()` registered?
- How would `MaxDepth` interact with an entity graph that has a back-reference (Article → Author →
  Article)?

---

## Architecture tests — enforcing dependency rules

**File:** none yet in this project
**Related:** NOTES3.md §2
**Date added:** 2026-10-21

### The confusion I had
- I thought "Domain must not reference infrastructure" was a convention you maintain by discipline and
  code review.
- It's a rule a test can enforce. Without one, it *will* erode — the finished course project already
  has `Submission.Domain/Behaviors/Person.cs:1: using Auth.Grpc;`.

### What to re-learn
- [ ] **`NetArchTest.Rules`** — `Types.InAssembly(...).That()....Should()....GetResult()`. Fluent, fast,
      no external tooling. This is the one I'd start with.
- [ ] **`ArchUnitNET`** — richer, closer to Java's ArchUnit; better error messages, steeper setup.
- [ ] **What to assert** (each is a real rule from NOTES3):
      - `*.Domain` must not depend on `*.Application`, `*.Persistence`, `*.API`, or any `*.Grpc` /
        `*.Contracts` namespace.
      - Types ending in `Response`/`Dto`/`Command`/`Query` must not depend on `*.Domain` (the
        deletable-Domain rule).
      - Nothing in `*.Domain` may reference `Microsoft.EntityFrameworkCore`.
      - Handlers must not depend on other handlers (slice isolation).
      - Endpoint classes must not reference repositories directly.
- [ ] **Why they're worth it**: they document the architecture in executable form, they fail in CI
      instead of in a design review six months later, and they make the "delete the Domain project"
      exercise a compiler-checked property rather than a hope.
- [ ] **The limitation**: they check *type* dependencies, not *semantic* ones. A handler can pass an
      entity through a `dynamic` or an object-typed dictionary and no architecture test will notice.

### Questions to test myself later
- How do I write the rule "no handler type may reference another slice's namespace" when slices are
  folders, not namespaces? (Hint: they *are* namespaces here — `Features.CreateArticle`.)
- What's the performance cost of scanning all assemblies, and does it belong in the unit test run or a
  separate CI gate?
- How would I assert a rule about *method* bodies (e.g. "no handler calls `SaveChanges` directly")? Is
  that in scope for these libraries at all?

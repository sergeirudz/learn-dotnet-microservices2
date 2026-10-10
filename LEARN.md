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

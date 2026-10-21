# `[MethodImpl(MethodImplOptions.NoInlining)]`

**Location in project:** `src/Articles/BuildingBlocks/Blocks.Core/Mapster/MapsterConfiguration.cs` — applied to `AddMapster()` which uses `Assembly.GetCallingAssembly()`.

## What inlining is

The JIT optimizer sometimes **inlines** a method call: instead of emitting a real call, it copies the small method's body directly into the caller. Great optimization for tiny hot-path methods (no call overhead, better register allocation) — and it happens invisibly; the JIT decides based on method size, tiering, and build configuration.

## Why inlining breaks `GetCallingAssembly()`

`Assembly.GetCallingAssembly()` doesn't use compile-time magic — it **inspects the real call stack at runtime** to answer: *"which assembly contains the method that called me?"*

Inlining erases a stack frame. If the JIT inlines `AddMapster` into its caller, then at the moment `GetCallingAssembly()` runs, *this method no longer exists on the stack* — its body is now part of the caller. So "who called me?" becomes "who called my *caller*?" → wrong assembly returned.

Concrete failure scenario:

```csharp
// Submission.API — Program.cs or DependencyInjection.cs
builder.Services.AddMapster();   // JIT inlines this call
```

After inlining, `GetCallingAssembly()` returns the assembly of whoever called *our* registration method, not the caller of `AddMapster`. `TypeAdapterConfig.GlobalSettings.Scan(assembly)` then scans the **wrong assembly** and silently skips all `IRegister` mapping profiles. No exception — just mappings that don't exist at runtime.

**The nastiest part:** Debug builds don't inline, Release builds do. So the bug only appears in production-like builds.

## What the attribute does

```csharp
[MethodImpl(MethodImplOptions.NoInlining)]
public static IServiceCollection AddMapster(this IServiceCollection services, Assembly? assembly = null)
```

A contract with the JIT: *never inline this method — in any tier, any build configuration.* The method keeps its own stack frame, so `GetCallingAssembly()` always reports the true caller — the assembly containing `services.AddMapster()`, exactly the one whose `IRegister` classes need scanning.

## Benefits

- **Correctness over micro-optimization** — the method runs once at startup; losing inlining costs nothing, the scan itself is far more expensive than call overhead.
- **Deterministic behavior** — same stack semantics in Debug and Release; no "works on my machine, breaks in prod" class of bug.
- **Enables the convenience overload** — callers can write `services.AddMapster()` with no arguments and still get the right assembly.

## When to use it

Use `NoInlining` when a method's **correctness depends on its own stack frame**:

1. **`Assembly.GetCallingAssembly()`** — the classic case (DI extension methods, auto-registration helpers).
2. **`StackTrace` / `StackFrame` inspection** — logging frameworks, diagnostic helpers that walk the stack.
3. **`CallerInformation`-adjacent runtime checks** — anything that reasons about "who invoked me" at runtime.

Related attribute worth knowing: `[MethodImpl(MethodImplOptions.AggressiveInlining)]` — the opposite hint ("please inline this even if you wouldn't naturally"). Only use for proven hot-path micro-optimizations; it's a hint, not a guarantee.

## When NOT to use it

- On normal business methods — you'd give up a real optimization for no reason.
- As a default "just in case" — only apply when the method inspects the stack or you've measured that inlining causes a problem.

## Key distinction: compile-time vs runtime caller info

- `CallerMemberName`, `CallerFilePath`, `CallerLineNumber` are resolved at **compile time** by the compiler → **safe with inlining**.
- `Assembly.GetCallingAssembly()`, `new StackTrace()`, `new StackFrame(1)` are resolved at **runtime** from the actual stack → **NOT safe with inlining**.

## Design alternative

The escape hatch is already in the signature: `Assembly? assembly = null`. If callers pass the assembly explicitly, the fragile path is never taken. A stricter design would make the parameter required and drop the attribute entirely — some libraries do exactly that to avoid this whole class of problem.

---

# Validated Options (`[Required]` + fail-fast at startup)

**Location in project:**
- Options class: `src/Articles/BuildingBlocks/Blocks.Messaging/RabbitMqOptions.cs`
- Registration: `src/Articles/Services/Submission/Submission.Api/DependencyInjection.cs` → `AddAndValidateOptions<RabbitMqOptions>(config)`
- Helper implementation: `src/Articles/BuildingBlocks/Blocks.Core/ConfigurationExtensions.cs`

## The problem it solves

Services read configuration (RabbitMQ credentials, gRPC endpoints, JWT secrets...). If a value is missing, you don't want to find out **hours into a running app** with a vague connection error — you want the app to **crash at startup** with a precise message. This is the "fail fast" principle.

## The three pieces

### 1. The options class with DataAnnotations

```csharp
public class RabbitMqOptions
{
    [Required] public string Host { get; set; } = "localhost";
    [Required] public string UserName { get; set; } = "guest";
    [Required] public string Password { get; set; } = "guest";
    [Required] public string VirtualHost { get; set; } = "/";
}
```

`[Required]` comes from `System.ComponentModel.DataAnnotations`. Other useful attributes: `[Range]`, `[MinLength]`, `[RegularExpression]`, `[Url]`.

⚠️ **Subtle gotcha:** `[Required]` on a property that has a default value (`= "guest"`) is NOT contradicted by the default — the default satisfies the validator. The `[Required]` here catches the case where the section exists but the key is **explicitly** null/empty in config. If the whole section is missing, the helper's `section.Exists()` check catches that first (see below). So the two checks complement each other.

### 2. The reusable helper (one place, every service reuses it)

```csharp
public static IServiceCollection AddAndValidateOptions<TOptions>(this IServiceCollection services,
    IConfiguration configuration) where TOptions : class
{
    var section = configuration.GetSection(typeof(TOptions).Name); // section named after the TYPE

    if (!section.Exists())
        throw new InvalidOperationException($"Configuration section '{section.Key}' is missing.");

    services
        .AddOptions<TOptions>()
        .Bind(section)                  // map config values onto the class
        .ValidateDataAnnotations()      // run [Required] etc. when options are resolved
        .ValidateOnStart();             // force resolution AT STARTUP, not on first use
    return services;
}
```

### 3. Registration in each service

```csharp
// Submission.Api/DependencyInjection.cs
services.AddAndValidateOptions<RabbitMqOptions>(config);
```

Convention: **section name = class name**. `appsettings.json`:

```json
{ "RabbitMqOptions": { "Host": "localhost", "UserName": "guest", ... } }
```

This is why the helper can use `typeof(TOptions).Name` — no magic strings anywhere, class renames flow through automatically.

## What each builder method actually does

| Method | Meaning |
|---|---|
| `AddOptions<T>()` | Registers the options infrastructure (`IOptions<T>`) in DI |
| `.Bind(section)` | Maps configuration section → object properties |
| `.ValidateDataAnnotations()` | Runs DataAnnotations validation **when options are resolved** |
| `.ValidateOnStart()` | Resolves options **immediately at app start** — if validation fails, the app crashes before serving a single request |

The key insight: `ValidateDataAnnotations()` alone is lazy — validation only runs when someone first injects `IOptions<T>.Value`. If nothing touches it until 30 minutes in, you find out 30 minutes in. **`ValidateOnStart()` forces that moment to be now.**

## What the failure looks like

```
Microsoft.Extensions.Options.OptionsValidationException:
DataAnnotation validation failed for 'RabbitMqOptions'
Required property 'Host' is missing.
```

Thrown during `builder.Build()` / at startup → container orchestrator (Docker, K8s) restarts or refuses to start the service. Compare with the alternative: app starts fine, then a consumer throws `RabbitMQ.Client.Exceptions` an hour later because `Host` was null.

## Consuming the options elsewhere

```csharp
// from DI
public class ArticlePublisher(IOptions<RabbitMqOptions> options) { ... }

// from IConfiguration directly (the other helper)
var grpcOptions = config.GetSectionByTypeName<GrpcServicesOptions>();
```

`GetSectionByTypeName<T>` does the same name convention but reads once, eagerly, no validation — good for values needed during registration itself (like gRPC client addresses).

## Pattern to remember

> Options class with `[Required]` attributes + generic `AddAndValidateOptions<T>` helper (`Bind` → `ValidateDataAnnotations` → `ValidateOnStart`) + section named after the class = fail-fast config with zero per-service boilerplate.

This is the standard .NET "Options pattern + startup validation" idiom, wrapped once in a BuildingBlock so all six services get it for free.

---

# How MassTransit DI uses `GetSectionByTypeName<RabbitMqOptions>`

**Location in project:**
- `src/Articles/BuildingBlocks/Blocks.Messaging/Masstransit/DependencyInjection.cs` → `AddMassTransitWithRabbitMQ`
- Helper: `Blocks.Core/ConfigurationExtensions.cs` → `GetSectionByTypeName<T>` (from previous section)

## The call

```csharp
public static IServiceCollection AddMassTransitWithRabbitMQ
    (this IServiceCollection services, IConfiguration configuration, Assembly assembly)
{
    var rabbitMqOptions = configuration.GetSectionByTypeName<RabbitMqOptions>(); // ← eager read

    var serviceName = assembly.GetServiceName();

    services.AddMassTransit(config =>
    {
        config.SetEndpointNameFormatter(new SnakeCaseWithServiceSuffixNameFormatter(serviceName));
        if (assembly != null)
            config.AddConsumers(assembly);

        config.UsingRabbitMq((context, rabbitConfig) =>
        {
            rabbitConfig.Host(new Uri(rabbitMqOptions.Host), rabbitMqOptions.VirtualHost, h =>
            {
                h.Username(rabbitMqOptions.UserName);
                h.Password(rabbitMqOptions.Password);
            });
            rabbitConfig.ConfigureEndpoints(context);
        });
    });
    return services;
}
```

## Why the *eager* helper here, not `AddAndValidateOptions<T>`?

This is the answer to "when do I use which?" — the two helpers exist because of **when** the values are needed:

| | `AddAndValidateOptions<T>` | `GetSectionByTypeName<T>` |
|---|---|---|
| Read moment | Lazily, when `IOptions<T>.Value` is first injected | **Immediately, during registration** |
| Validation | DataAnnotations + `ValidateOnStart` → fail-fast at startup | Only `Guard.AgainstNull` (section exists) — **no `[Required]` checks** |
| Use case | Options consumed later from DI (consumers, publishers) | Options needed **while building the registration itself** |

MassTransit is the textbook case for the second: `rabbitConfig.Host(...)` runs inside the registration callback that wires up the bus factory. The BuildingBlock needs the actual values **right now** — it can't wait for some service to inject `IOptions<RabbitMqOptions>` later. So it reads the section directly into a local variable and closes over it.

(`GetSectionByTypeName` still fails loudly if the whole `RabbitMqOptions` section is missing — `Guard.AgainstNull` — but if the section exists and `Host` is absent, you'd silently get the class default `"localhost"`. That's the price of the eager path: less safety. In practice the API layer usually calls `AddAndValidateOptions<RabbitMqOptions>` too, which covers the strict validation.)

## The rest of the method — what each piece does

### `assembly.GetServiceName()`

(From the finished project, `Blocks.Core/Extensions/AssemblyExtensions.cs` — being added in a later course video.)

```csharp
public static string GetServiceName(this Assembly assembly)
{
    var assemblyName = assembly.GetName().Name
        ?? throw new InvalidOperationException("Assembly has no name");
    return assemblyName.Split('.', StringSplitOptions.RemoveEmptyEntries)[0]  // "Submission.Api" → "submission"
        .Trim().ToLowerInvariant();
}
```

Takes the calling assembly's name (e.g. `Submission.Api`) and keeps only the first segment → `"submission"`. This gives every service a stable identity for message-queue naming.

### `SnakeCaseWithServiceSuffixNameFormatter`

⚠️ In the current course code this class is an **empty stub** — the finished project fills it in:

```csharp
public class SnakeCaseWithServiceSuffixNameFormatter : SnakeCaseEndpointNameFormatter
{
    readonly string _serviceName;
    public SnakeCaseWithServiceSuffixNameFormatter(string serviceName) { ... }

    public override string SanitizeName(string name)
    {
        name = name.Replace("EventHandler", "").Replace("Handler", "");
        name = base.SanitizeName(name);
        return $"{name}.{_serviceName}";
    }
}
```

It decides **queue names**. `ArticleApprovedHandler` in service `Submission` becomes queue `article_approved.submission`:

- strips the `EventHandler`/`Handler` suffixes (they're implementation detail, not identity)
- base formatter snake-cases the rest
- appends `.servicename` — so **each service gets its own queue** for the same event (competing-consumer style), instead of sharing one queue and randomly splitting messages between services

### `config.AddConsumers(assembly)` + `ConfigureEndpoints(context)`

- `AddConsumers` scans the passed assembly for `IConsumer<T>` implementations and registers them in DI.
- `ConfigureEndpoints` then auto-creates a receive endpoint (queue) per consumer, **named by the formatter above**, wired to that consumer.

## Pattern to remember

> **Options during registration** → `GetSectionByTypeName<T>` (eager, minimal validation).
> **Options consumed at runtime** → `AddAndValidateOptions<T>` (lazy + `ValidateOnStart` fail-fast).
> Same "section name = class name" convention powers both.

---

# `SnakeCaseWithServiceSuffixNameFormatter` — why the queue-name suffix matters

**Location in project:**
- Current (stub): `src/Articles/BuildingBlocks/Blocks.Messaging/Masstransit/SnakeCaseWithServiceSuffixNameFormatter.cs`
- Finished: `learn-microservices2-finished/src/BuildingBlocks/Blocks.Messaging/MassTransit/SnakeCaseWithServiceSuffixNameFormatter.cs`
- Wired in: `Blocks.Messaging/Masstransit/DependencyInjection.cs` → `SetEndpointNameFormatter(...)`
- Package: `MassTransit.RabbitMQ` 9.2.3

## The problem it solves: same event, multiple services

`config.ConfigureEndpoints(context)` auto-creates **one queue per consumer**, named by the endpoint name formatter. Two services that both consume the same integration event would both compute the **same queue name** — e.g. plain snake-case gives `article_approved_for_review` in *both* ArticleHub and Review.

RabbitMQ queues are identified by name. Two services declaring a queue with the same name = **one shared queue**: messages round-robin between the two services — each event delivered to *only one* of them, arbitrarily. For pub/sub that's silently wrong: an event that both services need gets eaten by whichever service won the queue-declaration race.

**The fix:** append the service name to every queue name. The finished project proves the payoff — `ArticleApprovedForReviewEvent` is consumed by **three** services, so the broker ends up with three independent queues, each bound to the same event exchange:

| Service | Queue | Gets |
|---|---|---|
| ArticleHub | `article_approved_for_review.articlehub` | its own copy |
| Journals | `article_approved_for_review.journals` | its own copy |
| Review | `article_approved_for_review.review` | its own copy |

Every service receives **every** event (fan-out). Within *one* service, running multiple instances means multiple consumers on that service's *own* queue → round-robin between instances (competing consumers). Both semantics are correct at once — that's the whole benefit in one design.

Secondary wins:
- **Readable, deterministic names** — snake_case, no GUIDs/version junk; same name on every restart.
- **No magic strings** — queue names derive from consumer type names; renaming flows through.
- **Implementation details stripped** — `Handler`/`EventHandler` suffixes say *how* a message is processed, not *what* it is; the formatter removes them from the wire name.

## How it's implemented (finished version)

```csharp
public class SnakeCaseWithServiceSuffixNameFormatter : SnakeCaseEndpointNameFormatter
{
    readonly string _serviceName;

    public SnakeCaseWithServiceSuffixNameFormatter(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name must be provided", nameof(serviceName));
        _serviceName = serviceName.Trim().ToLowerInvariant();
    }

    public override string SanitizeName(string name)
    {
        name = name
            .Replace("EventHandler", "")   // order matters: EventHandler FIRST
            .Replace("Handler", "");
        name = base.SanitizeName(name);    // snake_case from MassTransit's base class
        return $"{name}.{_serviceName}";   // the whole point: per-service queue
    }
}
```

Walking through `ArticleApprovedForReviewConsumer` + `Assembly.GetExecutingAssembly()` (assembly name `ArticleHub.API`):

1. `GetServiceName()` → `articlehub` (first dot-separated segment of the assembly name, lowercased)
2. `ConfigureEndpoints` asks the formatter for the consumer's endpoint name
3. `SanitizeName("ArticleApprovedForReviewConsumer")` → base formatter already trims the `Consumer` suffix → `ArticleApprovedForReview` → snake-case → `article_approved_for_review` → **`article_approved_for_review.articlehub`**

Two subtle details worth remembering:

- **Replace order matters.** `"EventHandler"` must be replaced before `"Handler"`, because `"EventHandler"` *contains* `"Handler"`. Reversed, `ArticleApprovedEventHandler` → `ArticleApprovedEvent` → queue `article_approved_event` (event-ness leaks into the name); correct order → `article_approved.review`.
- **The `Handler` replaces are mostly defensive.** MassTransit's name formatter pipeline already trims the `Consumer` suffix itself, and this repo names consumers `*Consumer`. The replaces exist to also support the `*Handler` naming style — belt and braces. The service suffix is the part doing real work here.

## How it's wired in this repo

```csharp
// Blocks.Messaging/Masstransit/DependencyInjection.cs
var serviceName = assembly.GetServiceName();                    // "submission"

services.AddMassTransit(config =>
{
    config.SetEndpointNameFormatter(
        new SnakeCaseWithServiceSuffixNameFormatter(serviceName));  // register FIRST

    if (assembly != null)
        config.AddConsumers(assembly);                              // find IConsumer<T>s

    config.UsingRabbitMq((context, rabbitConfig) =>
    {
        rabbitConfig.ConfigureEndpoints(context);                   // queue per consumer, named by formatter
    });
});
```

Callers pass `Assembly.GetExecutingAssembly()` (e.g. `Submission.Application/DependencyInjection.cs`), so each service auto-registers *its own* consumers under *its own* name. Formatter must be set before `ConfigureEndpoints` runs — that's why it's the first call inside `AddMassTransit`.

⚠️ Course status: in the current code the class is an **empty stub** (`public class SnakeCaseWithServiceSuffixNameFormatter { }`) — it gets fleshed out in a later video. Until then `SetEndpointNameFormatter` receives an object that isn't an `IEndpointNameFormatter` at all, so auto-endpoint naming isn't functional yet.

## Pattern to remember

> Queue name = `sanitize(consumer type name) + "." + service name`. Per-service queues turn one shared round-robin queue (broken pub/sub) into independent fan-out queues (correct pub/sub) — while multiple instances *within* a service still compete on their own queue. One custom `SanitizeName` override, set via `SetEndpointNameFormatter`, fixes both.

---

# `EmailAddress` value object — implicit operators + equality trio

File: `src/Articles/Services/Review/Review.Domain/_Shared/ValueObjects/EmailAddress.cs`
(also see finished project pattern: DDD value objects wrapping a primitive + factory validation)

## What the code does

```csharp
public static implicit operator EmailAddress(string value) => Create(value);

public static implicit operator string(EmailAddress email) => email.Value;

public static bool operator ==(EmailAddress a, string b)
{
    if (ReferenceEquals(a, null) && ReferenceEquals(b, null)) return true;
    if (ReferenceEquals(a, null) || ReferenceEquals(b, null)) return false;
    return string.Equals(a.Value, b, StringComparison.OrdinalIgnoreCase);
}

public static bool operator !=(EmailAddress a, string b) => !(a == b);

public override int GetHashCode()
    => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
```

## The four pieces

### 1. `string` → `EmailAddress` (implicit)

```csharp
EmailAddress email = "sergei@example.com";  // compiler calls Create(...)
```

`Create` is the factory with validation/normalization, so an invalid email throws at
*assignment* instead of a bad value leaking deeper into the domain. "Parse, don't validate"
— after construction, the object is guaranteed valid.

### 2. `EmailAddress` → `string` (implicit)

```csharp
string raw = email;               // instead of email.Value
await SendEmailAsync(email);      // method taking string just works
```

Type-safe at the boundary, string-shaped at the edges.

### 3. `==` / `!=` against `string`

```csharp
if (person.Email == "sergei@example.com")  // compiles thanks to the operator
```

- Comparison is `StringComparison.OrdinalIgnoreCase` — emails are conventionally
  case-insensitive, and this class treats the whole address that way.
- The `ReferenceEquals` dance exists because `a.Value` would throw NRE if `a` is null.
  Both null → equal; one null → not equal.
- C# rule: defining `==` forces `!=` too, and you must also override `Equals(object)`
  and `GetHashCode`, otherwise `Equals` and `==` can disagree.

### 4. `GetHashCode`

```csharp
public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);
```

Hash must **match the equality rule**: equal objects → equal hashes. Since `==` ignores
case, the hash must too — otherwise `EmailAddress` breaks as a `Dictionary` key /
`HashSet` member (two "equal" items in different buckets). `==`, `Equals`, `GetHashCode`
are a trio that must tell the same story.

## Benefits (why do this at all)

- **Type safety with zero ceremony** — method signatures say `EmailAddress`, so you can't
  accidentally pass an order ID or an unvalidated string, yet call sites read naturally.
- **Validation happens once, at the door** — implicit conversion routes every creation
  through `Create`, so no unvalidated email can exist.
- **Domain behavior gets a home** — the type can later grow methods (masking, domain-part
  extraction) that a raw string can never hold.
- **Interop with string-based APIs** (EF Core mapping, email senders, serializers) without
  `.Value` noise everywhere.

## The trade-off to remember

`implicit` hides *where* conversion happens. Guidance: implicit only when conversion can
never fail — but email validation *can* throw. This is a deliberate readability trade-off
the course makes; the stricter style is `explicit` (or always calling `Create` explicitly),
so validation is visible at every call site. Both are valid; be aware which one you chose.

## Pattern to remember

> Value object = wrapper type + `Create` factory + implicit conversions both ways +
> `==`/`!=`/`Equals`/`GetHashCode` all following the same equality rule (here:
> case-insensitive). The object behaves like a string at the edges but is validated and
> type-safe everywhere inside.

---

# `TypeDiscriminator` — EF Core TPH inheritance made visible to the domain

File: `src/Articles/Services/Review/Review.Domain/_Shared/Person.cs` (base declaration)
Files: `src/Articles/Services/Review/Review.Domain/Articles/Reviewer.cs`, `.../Articles/Editor.cs` (overrides)
Finished-project proof: `learn-microservices2-finished/src/Services/Review/Review.Persistence/Migrations/20250925115642_InitialCreate.cs` (column + `HasDiscriminator` + unique index)

## What it is

EF Core's **table-per-hierarchy (TPH)** inheritance strategy stores an entire class
hierarchy in **one table**, with a special column — the *discriminator* — tagging which
concrete class each row is. Here the tree is:

```
Person           ← mapped to table "Person"
├── Author
└── Reviewer
    └── Editor   ← Editor IS-A Reviewer
```

The domain declares it as a real (not shadow) property:

```csharp
// Person.cs:20
public virtual string TypeDiscriminator { get; init; } = null!; // EF discriminator

// Reviewer.cs:11 / Editor.cs:7
public override string TypeDiscriminator => nameof(Reviewer);
public override string TypeDiscriminator => nameof(Editor);
```

Result in the database (from finished migration):

```csharp
TypeDiscriminator = table.Column<string>(type: "nvarchar(8)", maxLength: 8, ...)
b.HasDiscriminator<string>("TypeDiscriminator")
    .HasValue("Person").HasValue("Author").HasValue("Editor").HasValue("Reviewer");
```

Every `Person` row carries its class name; EF reads it when materializing rows.

## What the query becomes

```csharp
context.People.OfType<Editor>()
// SELECT * FROM Person WHERE TypeDiscriminator = 'Editor'
```

## Why use it (vs alternatives)

| Strategy | Tables | How row → class |
|---|---|---|
| **TPH** (used here) | 1 per hierarchy | discriminator column |
| TPT (table-per-type) | 1 per class, joined | JOINs, no column |
| Table-per-concrete | 1 per concrete class | no inheritance in DB |

TPH is EF's default because it's fastest — no JOINs — at the cost of nullable subclass
columns and one tag column. The discriminator is that tag.

## Why declare it as a *real* property (not shadow)?

EF creates a hidden **shadow property** automatically from `HasDiscriminator()`.
Declaring it explicitly buys:

1. **Domain code reads it without casting** — `person.TypeDiscriminator == "Editor"`
   works on a `Person` reference (logging, mapping, role checks before you have the typed
   instance).
2. **The value lives beside the class** — each subtype owns its tag via
   `nameof(Editor)` (compile-time safe: rename class → string updates). With a shadow
   property, the same strings live only in persistence config.

Details: `virtual` so subclasses override; `init`; base is `null!` because EF sets it at
materialization / the override supplies it.

## Benefits

- **Type information survives round-trips** — a row becomes the right class automatically.
- **Polymorphic queries are cheap** — `OfType<T>()` is a simple WHERE on the column.
- **Domain-visible role tag** — check "is this person an Editor?" without a cast.
- **Enables per-type constraints** — see the unique index below.

## Use cases in this repo

- **The unique index trick** (finished migration):
  `CREATE UNIQUE INDEX IX_Person_Email_TypeDiscriminator ON Person (Email, TypeDiscriminator)`
  — the same human can be both a Reviewer and an Author (same email, two rows), and
  uniqueness is enforced *per role*. Two Reviewers with one email are still blocked.
  That index is only natural *because* the discriminator is a real column.
- **Event/actor tracking** — the `ArticleActor` entity (separate hierarchy in the same
  migration) also uses a `TypeDiscriminator`, same pattern.

## Gotchas

- `HasDiscriminator().HasValue(...)` strings must **match** the `nameof(...)` overrides
  exactly, or EF throws "unknown discriminator value" at materialization. `nameof` keeps
  them in sync as long as config uses the same source.
- All hierarchy rows share the table, so subclass columns are nullable and cannot be
  `required` in the DB (enforce via domain instead).
- A discriminator is immutable by design — EF doesn't support changing an existing row's
  type; delete + insert instead.

## Pattern to remember

> TPH = one table per hierarchy + discriminator column tagging each row's class.
> Exposing it as a mapped `virtual` property (each subtype overrides with
> `nameof(ConcreteClass)`) lets the domain read the row's type without casting and puts
> the tag value beside the class instead of in hidden EF shadow state. Bonus: including
> the discriminator in unique indexes gives per-type uniqueness for free.

---

# `internal` backing field + `IReadOnlySet` — protecting who sets a collection and when

File: `src/Articles/Services/Review/Review.Domain/Articles/ArticleAuthor.cs`

## The code

```csharp
public class ArticleAuthor : ArticleActor
{
    internal HashSet<ContributionArea> _contributionAreas = new();
    public IReadOnlySet<ContributionArea> ContributionAreas => _contributionAreas;
}
```

(Note: the fields are `enum ContributionArea` values from
`BuildingBlocks/Articles.Abstractions/Enums/ContributionArea.cs`; base class
`ArticleActor` holds ArticleId / PersonId / Role.)

## What each piece does

- **`internal` on the backing field** — the field is visible only inside the
  `Review.Domain` assembly. External callers can't even *reach* the mutable
  `HashSet<ContributionArea>`; there is no public setter to misuse.
- **`public` property exposing `IReadOnlySet`** — outside code can read/enumerate the
  collection but gets **no `Add`/`Remove`** methods on the static type. The interface
  itself omits the mutating members.
- **Expression-bodied property (no `init` setter)** — the property is a *view* over the
  field, so external code can't reassign it either.

Contrast with the commented-out line (`public HashSet<ContributionArea> { get; init; }`)
— that version lets **any** caller construct an `ArticleAuthor` and hand it whatever
set they like, bypassing all domain rules.

## Who can set it, and when

With this shape there are exactly two ways to populate `_contributionAreas`:

1. **Inside `Review.Domain`** — e.g. a domain method on the aggregate
   (`article.AssignAuthor(...)` style) that validates first, then mutates
   `_contributionAreas`. Only domain code that owns the rules can change the collection,
   *after* those rules pass.
2. **EF Core at materialization** — EF navigates the property/field via its mapping and
   populates it when loading from the DB. EF isn't bound by C# access modifiers the same
   way; it can use backing fields directly (backing-field mapping).

The commented-out constructor stub shows the intended third door: a constructor (or
factory) parameter — also fine, since it runs *inside* the assembly and can validate
before assigning. So: **set = domain logic (inside assembly) or EF; never external code.**

## Why this matters for DDD aggregates

An aggregate root must protect its invariants. If `ContributionAreas` were a public
`init` set, then any Application-layer handler could do:

```csharp
new ArticleAuthor { ContributionAreas = new HashSet<ContributionArea> { ... } };  // no rules checked
```

With `internal` + `IReadOnlySet`, mutation must go through domain methods, so rules like
"every author needs ≥1 contribution area" (see the commented `MandatoryAreas` check in
`Submission.Domain/Behaviors/Article.cs`) get enforced in exactly one place.

⚠️ Repo note: this mirrors what `Submission.Domain/Entities/ArticleAuthor.cs` does with
`AssignAuthor` — there the set is populated via a domain method on the `Article`
aggregate, which is the "who/when" enforcement in action. In the *finished* project this
class was later simplified back to `public init` (course simplification), so don't be
surprised when the video diverges.

## Two subtleties worth knowing

- **`IReadOnlySet` is a wrapper, not a copy.** If some internal code hands the HashSet out
  (or the exposed view is cast back via reflection / `(HashSet<T>)` on a downcast), it
  *can* be mutated. It's a compile-time contract, not a security boundary. (Freezing via
  `ImmutableHashSet` or `FrozenSet` is the airtight version.)
- **`internal` reaches further than the assembly when `InternalsVisibleTo` is used** —
  this repo declares none for Review.Domain (checked), so the boundary holds. Adding
  `[assembly: InternalsVisibleTo("Some.Tests")]` is the common, legitimate way to let a
  test project in without making things public.

## Pattern to remember

> Encapsulated collection = `internal` mutable field + public `IReadOnly*` view +
> mutation only through domain methods. Access modifiers decide **who** can change it
> (assembly-internal), domain methods decide **when** (after invariants pass), EF remains
> able to hydrate it via backing-field mapping. Public `init` on a collection is usually
> an aggregate-invariant leak.

---

# `EnumEntity<TEnum>` — the "enumeration class" pattern (enums + tables mixed)

File: `src/Articles/BuildingBlocks/Blocks.Domain/Entities/EnumEntity.cs`
Used by: `src/Articles/Services/Review/Review.Domain/Assets/AssetTypeDefinition.cs` (Review) and
`src/Articles/Services/Submission/Submission.Domain/Entities/AssetTypeDefinition.cs` (Submission)
EF config: `src/Articles/Services/Submission/Submission.Persistence/EntityConfigurations/AssetTypeDefinitionEntityConfiguration.cs`
Repo comment in source: `//insight - mix enums & tables togheter`

## The code

```csharp
// EnumEntity.cs
public abstract class EnumEntity<TEnum> : Entity<TEnum>
    where TEnum : struct, Enum
{
    public TEnum Name { get; init; } = default!;
}
```

The key: `EnumEntity<TEnum>` passes `TEnum` **up** to `Entity<TEnum>` as the primary-key
type. Substituting `AssetTypeDefinition : EnumEntity<AssetType>` expands to:

```csharp
public AssetType Id { get; init; }     // TPrimaryKey = TEnum = AssetType
public AssetType Name { get; init; }   // TEnum = AssetType
public required FileExtensions AllowedFileExtensions { get; init; }
public required byte MaxAssetCount { get; init; }
public required byte MaxFileSizeInMB { get; init; }
public int MaxFileSizeInBytes => MaxFileSizeInMB * 1024 * 1024;
public bool AllowsMultipleAssets => MaxAssetCount > 1;
```

So "Name will be AssetType" is only the visible half — **`Id` becomes `AssetType` too**.
The row is keyed by the enum value itself; no surrogate key to keep in sync.

## What the table looks like

One row per enum member (`HasEnumConversion()` on `Name`, unique index):

| Id | Name | AllowedFileExtensions | MaxAssetCount | MaxFileSizeInMB |
|----|------|----------------------|---------------|-----------------|
| 0 | Manuscript | [..., "pdf"] | 1 | 50 |
| 1 | SupplementaryMaterial | [...] | 10 | 25 |

## Why use it — the problem it solves

The enum alone gives *identity*; the table gives each member *rich configuration* without
scattering rules across switch statements:

```csharp
// Without the table — rules hardcoded in code:
var maxCount = assetType switch { AssetType.Manuscript => 1, ... };

// With the table — rules are data:
var def = context.AssetDefinitions.Single(d => d.Name == assetType);
if (assets.Count >= def.MaxAssetCount) throw ...;
```

## Benefits

- **New enum member = new row**, not new code branches — upload rules, limits, allowed
  extensions all come from data (seeded, editable).
- **Type-safe keys** — `Id`/`Name` are `AssetType`, not magic `int`/`string` that can
  drift from the enum. `where TEnum : struct, Enum` enforces it at compile time.
- **PK is the enum value** — lookups trivially unique, no sync between key and identity.
- **Constraint enforcement in one place** — `MaxAssetCount`, `MaxFileSizeInMB` live beside
  the type they govern, not in handlers.
- This is Fowler's classic **"Enumeration Class"** pattern done via generics: enum
  members promoted to entities so they can carry behavior/data.

## Pattern to remember

> `EnumEntity<TEnum> : Entity<TEnum>` — passing the enum as the generic argument types
> *both* `Name` and the inherited `Id` as that enum. The class becomes an enumeration
> class: a DB row per enum member, keyed by the enum value itself, holding per-member
> metadata that would otherwise rot in switch statements.

---

# How one table can hold multiple entities (EF Core mapping strategies)

**Location in project:**
- `src/Articles/Services/Review/Review.Persistence/EntityConfigurations/PersonEntityConfiguration.cs` — `HasDiscriminator` wiring
- `src/Articles/Services/Review/Review.Domain/_Shared/Person.cs` + `Articles/Author.cs`, `Reviewer.cs`, `Editor.cs` — one hierarchy, four classes
- `src/Articles/Services/Review/Review.Domain/Articles/ArticleActor.cs` + `ArticleAuthor.cs` — second hierarchy in the same service
- Finished-project proof: `learn-microservices2-finished/src/Services/Review/Review.Persistence/Migrations/20250925115642_InitialCreate.cs` (one `Person` table, merged columns)

## The mental model

An **entity** is a CLR class; a **table** is storage. EF Core's mapping between them is not forced to be 1:1 — several classes can share one table. There are three distinct mechanisms, and they solve different problems:

| Mechanism | Classes involved | Row → object | In this repo |
|---|---|---|---|
| **TPH inheritance** | one hierarchy (base + subclasses) | discriminator column picks the class | `Person` / `Author` / `Reviewer` / `Editor`; `ArticleActor` / `ArticleAuthor` |
| **Shared table (table splitting)** | two *unrelated* types | same PK, each type maps disjoint columns of the same row | (not used yet — see below) |
| **Owned / complex types inline** | parent + its value-object children | child has no row at all — flattened into the parent's row | `Person.Email` via `ComplexProperty` |

### 1. TPH — the main case: one inheritance tree, one table

```csharp
// PersonEntityConfiguration.cs
builder.HasDiscriminator(e => e.TypeDiscriminator)
    .HasValue<Person>(nameof(Person))
    .HasValue<Author>(nameof(Author))
    .HasValue<Reviewer>(nameof(Reviewer))
    .HasValue<Editor>(nameof(Editor));
```

The whole tree collapses into one `Person` table. Every row carries `TypeDiscriminator`, and EF reads it to decide which class to instantiate when materializing. Two details visible in the finished migration:

- **Columns are the *union* of all classes in the tree.** The `Person` table holds `Degree` and `Discipline` (Author-only) alongside `FirstName`/`LastName` (base). Subclass columns are therefore **nullable in the DB even when `required` in C#** — a `Reviewer` row leaves `Degree` NULL. Database-level `NOT NULL` for subclass properties is impossible under TPH; invariants stay in the domain.
- **No `DbSet` needed per subclass.** Only `DbSet<Person>` exists; EF discovers `Author`/`Reviewer`/`Editor` through the class hierarchy. The table is named after the *base* type.

Queries:

```csharp
context.People                     // SELECT * FROM Person (all four classes come back, each as its right type)
context.People.OfType<Editor>()    // SELECT * FROM Person WHERE TypeDiscriminator = 'Editor'
```

⚠️ If you *never* configure `HasDiscriminator`, EF still picks TPH by default — but with a hidden **shadow** discriminator column and an int-typed discriminator value. Making `TypeDiscriminator` a real mapped `string` property (see the previous note) is what makes the tag visible to the domain and indexable.

### 2. Shared table — two unrelated classes, one row used by both

TPH requires a common base class. To store **unrelated** types in one table, both simply declare the same table name and map disjoint columns:

```csharp
builder.Entity<ActiveUser>(b => {
    b.ToTable("Users");
    b.Property(u => u.Email).HasColumnName("Email");
});
builder.Entity<DeletedUser>(b => {
    b.ToTable("Users");                       // same table
    b.Ignore(u => u.SomethingElse);           // columns must not overlap
});
```

Each row is shared by one instance of each type, matched by PK. Real-world use: a wide table you want to query through two narrow "views" (e.g. a read model exposing only a subset of columns), or legacy schemas. Not used in this project — knowing it exists explains why EF forbids overlapping column names between entity types mapped to the same table.

### 3. Owned / complex types — child objects flattened into the parent's row

```csharp
builder.ComplexProperty(o => o.Email, b =>
    b.Property(n => n.Value).HasColumnName("Email").HasMaxLength(MaxLength.C64));
```

`EmailAddress` is an entity in the model sense (it has properties) but has **no identity and no row** — its `Value` becomes an `Email` column on `Person`. So the *table* holds data of several types, but only one is an entity with a key. The inverse operation also exists: **entity splitting** (`SplitToTable`) maps *one* entity across *several* tables joined by PK.

## Why TPH (vs the alternatives)

| Strategy | Tables | Row → class | Cost |
|---|---|---|---|
| **TPH** (used here) | 1 per hierarchy | discriminator column | nullable subclass columns; wide table |
| TPT | 1 per class | JOIN on PK | JOIN cost on every query |
| TPC | 1 per concrete class | UNION queries | polymorphic queries get expensive |

TPH is EF's default because the common case — "load some people" — never leaves one table: no JOINs, no UNIONs. The trade is schema purity (columns nullable in DB) for query speed.

## Gotchas

- Discriminator values in `HasValue<T>(...)` must match what the property actually holds (here: `nameof` of each class) — mismatch throws *"unknown discriminator value"* at materialization, not at startup.
- The discriminator is **immutable**: EF cannot change a row's class. A Reviewer becoming an Editor = delete + insert.
- Can't mix hierarchies freely: `Editor : Reviewer : Person` means an `Editor` row also satisfies `OfType<Reviewer>()` — the tree shape determines query results.
- All TPH configuration happens on the **base** type's configuration (`PersonEntityConfiguration`); per-subclass config classes are still possible (`builder.Entity<Author>()`) but column constraints on shared properties belong on the base.

## Pattern to remember

> Several entities share one table in three ways: **TPH** (one hierarchy + discriminator column picks the class; subclass columns become nullable; query subclasses with `OfType<T>()`), **shared table** (unrelated types, disjoint columns, one PK per row), and **owned/complex types** (no row of their own — flattened into the parent's row). TPH wins on query speed; the price is that "required" stops being enforceable at the database level for subclass properties.

---

# DDD Aggregate structure in this repo — blueprint for reuse in future projects

Course video: "ArticleApproved Domain Aggregates with DDD" (Review service domain model)
Domain under study: `src/Articles/Services/Review/Review.Domain/` (uncommitted changes as of 2026-10-12)
Building blocks: `src/Articles/BuildingBlocks/Blocks.Domain/`

## The full file map (what connects to what)

```
Blocks.Domain/                       ← reusable base classes (no domain knowledge)
├── Entities/IEntity.cs              Entity (int Id) / Entity<TPrimaryKey>
├── Entities/AggregateEntity.cs      Entity + domain-events plumbing (IAggregateEntity)
├── ValueObjects/IValueObject.cs     marker interface
└── ValueObjects/StringValueObject.cs base for string-typed VOs

Review.Domain/
├── Articles/Article.cs              ★ AGGREGATE ROOT : AggregateEntity
│      private List<ArticleActor> _actors      → IReadOnlyList
│      private List<Asset> _assets             → IReadOnlyList
│      Stage { get; private set; }             ← state transitions guarded by root
│      JournalId (int) + Journal navigation    ← reference OUT by ID
├── Articles/ArticleActor.cs         child entity : Entity (ArticleId, PersonId, Role,
│                                    TypeDiscriminator for TPH)
├── Articles/ArticleAuthor.cs        child entity : ArticleActor (internal _contributionAreas
│                                    + public IReadOnlySet — encapsulated collection)
├── _Shared/Person.cs                outside entity : Entity (TPH base: Author/Reviewer/Editor)
├── Articles/Reviewer.cs, Editor.cs  outside entities (referenced via PersonId, never owned)
├── Articles/Journal.cs              outside entity : Entity (referenced via JournalId)
└── Assets/
    ├── Asset.cs                     child entity of Article (ArticleId + virtual Article;
    │                                 private setters; File VO embedded)
    ├── AssetTypeDefinition.cs       EnumEntity<AssetType> — enumeration class (config data)
    └── ValueObjects/                File, FileName, FileExtension(s), AssetName — value
                                      objects owned by Asset (no identity of their own)
```

## The pattern in code (condensed reference)

**Base classes** (write once per solution):

```csharp
public abstract class Entity { public virtual int Id { get; init; } }

public interface IAggregateEntity : IEntity
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public class AggregateEntity : Entity, IAggregateEntity
{
    private List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;
    protected void AddDomainEvent(IDomainEvent eventItem) => _domainEvents.Add(eventItem);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

public interface IValueObject;   // marker
```

**The aggregate root** — owns collections, guards state, references others by ID:

```csharp
public partial class Article : AggregateEntity
{
    public required string Title { get; init; }          // immutable facts
    public ArticleStage Stage { get; private set; }      // guarded transitions
    public required int JournalId { get; init; }         // OUT-reference by ID
    public Journal Journal { get; init; } = null!;       // read-only nav for outsiders

    private readonly List<ArticleActor> _actors = new();
    public IReadOnlyList<ArticleActor> Actors => _actors.AsReadOnly();  // owned children

    private readonly List<Asset> _assets = new();
    public IReadOnlyList<Asset> Assets => _assets.AsReadOnly();
    // mutation only via domain methods added later (AssignAuthor, UploadAsset, ...)
}
```

**Child entity** — identity, but only reachable through the root:

```csharp
public class ArticleActor : Entity
{
    public int ArticleId { get; init; }                  // FK to owning root
    public int PersonId { get; init; }                   // OUT-reference by ID
    public Person Person { get; init; } = null!;
    public UserRoleType Role { get; init; }
    public string TypeDiscriminator { get; init; } = null!;  // TPH: ArticleAuthor etc.
}
```

**Value object** — no identity, validated at the door:

```csharp
public class AssetName : StringValueObject
{
    [JsonConstructor] private AssetName(string value) => Value = value;  // private ctor

    public static AssetName Create(AssetTypeDefinition assetType, int assetCount) { ... }
}
```

## How to classify any new class (the decision procedure)

1. **Does it have identity that survives changes?** No → **value object** (validate in
   factory, private ctor, `readonly`/`init` members).
2. **Are its invariants enforced by another aggregate's rules?** Yes → **child entity**:
   private/internal mutable state, public read-only view, FK to the root, created/mutated
   only by root domain methods.
3. **Does it have its own invariants and lifecycle?** → **outside entity**: reference by
   ID from the aggregate, never as a mutable navigation.
4. **Is it the thing handlers load, mutate, and save in one transaction?** → that's the
   **aggregate root**.

Test question for the boundary: "which invariants must be true at the END of every
transaction?" Everything those invariants span = one aggregate. Everything else = outside.

## Benefits

- **One transactional consistency boundary** — invariants enforced in exactly one place
  (the root), so no handler can bypass them (cf. the `internal _contributionAreas` note).
- **Cheap loads, no accidental graphs** — reference-by-ID for outside entities keeps
  aggregates small; EF `.Include()` only what the aggregate owns.
- **Domain events built in** — `AggregateEntity` queues events on state change; publisher
  dispatches after save (ties into the MassTransit notes above).
- **Compile-time protection** — `IReadOnlyList` views + `private set` make illegal states
  hard to express from outside the assembly.
- **Reusable vocabulary** — same `Entity`/`AggregateEntity`/`IValueObject` bases serve
  every service, so each service's domain reads the same way.

## Base-class choice table (this repo vs finished project)

| Class | This repo (current video) | Finished project | Note |
|---|---|---|---|
| `Article` | `AggregateEntity` | `AggregateRoot` | same role: root |
| `Asset` | `AggregateEntity` | `AggregateRoot` | finished promotes Asset to its own root (assets are manipulated directly by ID — upload/download endpoints) |
| `Person` | `Entity` | `AggregateRoot` | finished treats Person as its own root |
| `ArticleActor` | `Entity` | `Entity` | child in both |
| Domain-events base | `AggregateEntity` | `AggregateRoot` (auditing fields too) | naming differs, plumbing identical |

The lesson: **aggregate membership is a manipulation-boundary decision, not a class-diagram
truth.** Asset is "child of Article" on paper but a root in the finished code because the
UI targets assets directly. Redraw boundaries when a different transaction starts editing
something independently.

## Checklist for a new project

- [ ] `Blocks.Domain`-style base: `Entity`, `AggregateEntity`/`AggregateRoot` (domain
      events), `IValueObject` + `StringValueObject`
- [ ] One aggregate root per consistency boundary, inheriting the aggregate base
- [ ] Child entities: FK to root, `private set`, exposed via `IReadOnly*` over private
      field; mutated only by root domain methods
- [ ] Value objects: factory `Create`/`From` with validation, private ctor, marker interface
- [ ] Outside entities: referenced by ID (+ read-only navigation for display)
- [ ] EF config: backing-field access for `_actors`/`_assets`, TPH discriminators where
      child entities have subtypes
- [ ] Domain methods on the root raise `AddDomainEvent` → publisher dispatches after save

## Pattern to remember

> Aggregate = root (`AggregateEntity`/`AggregateRoot`, owns collections as private fields
> + `IReadOnly*` views, raises domain events) + child entities (FK to root, no outside
> reach) + value objects (factory-validated, no identity). Outsiders are referenced by ID.
> The boundary is drawn around invariants that must hold per transaction — and is redrawn
> whenever a new manipulation needs direct access (Asset: child → root).

---

# Adding restrictions to Entities — how this project does it + the popular alternatives

**Files in this project (the whole toolbox):**
- DB-level: `src/Articles/BuildingBlocks/Blocks.EntityFrameworkCore/EntityConfigurations/EntityConfiguration.cs` (base),
  `Services/Review/Review.Persistence/EntityConfigurations/PersonEntityConfiguration.cs` / `ArticleEntityConfiguration.cs`
- Length registry: `src/Articles/BuildingBlocks/Blocks.Core/Constraints/MaxLength.cs`
- Runtime guards: `src/Articles/BuildingBlocks/Blocks.Core/Guard.cs`
- Domain validation: `Services/Review/Review.Domain/_Shared/ValueObjects/EmailAddress.cs` (VO factory)
- C#-level: `Services/Review/Review.Domain/_Shared/Person.cs` (`required`, `init`, nullable `int?`)

## The core idea: restrictions live in *layers*, and each layer catches what the others can't

A "restriction" (max length, required, valid email, positive count) can be enforced at
several moments. This project spreads them deliberately:

| Layer | When it fires | What it enforces | Example here |
|---|---|---|---|
| **C# type system** | compile time | can't even *construct* an invalid object | `required`, `init`, `int?` |
| **Domain (Guard + VO factories)** | object creation / domain method | business rules, once at the door | `EmailAddress.Create` |
| **EF Fluent config** | `SaveChanges` / migration | DB schema: lengths, nullability, indexes, FKs | `HasMaxLength(MaxLength.C64)` |
| **Database** | insert/update | last line of defense, data lives longest | `nvarchar(64)` column, unique index |

Rule of thumb: **the earlier the layer, the cheaper the failure.** A compile error beats a
guard exception beats a `DbUpdateException` beats corrupt data in a table nobody noticed
for months.

## How it's done in this project (four mechanisms)

### 1. C# type system — make illegal states unrepresentable

```csharp
// Person.cs
public required string FirstName { get; init; }   // MUST be set at construction; immutable after
public int? UserId { get; init; }                 // explicitly optional, not "string that's sometimes null"
public virtual string TypeDiscriminator { get; init; } = null!;  // EF sets it; domain never does
```

- **`required`** — compiler refuses `new Person { LastName = ... }` without `FirstName`.
  Cheapest possible validation: it never runs, because invalid code never compiles.
- **`init`** — value frozen after construction; nobody can mutate an entity into an
  invalid state by assignment (cf. the `internal`-field note: mutation goes through
  domain methods instead).
- **Nullable annotations** — `int?` says "optional by design"; `string` (non-nullable)
  says "null is a bug", enforced by compiler warnings.
- **`IReadOnly*` views over private fields** (`Reviewer._specializations`,
  `ArticleAuthor._contributionAreas`) — outsiders can read collections but can't `Add`.

### 2. Domain guards + value-object factories — business rules fire once, at the door

```csharp
// Guard.cs — thin wrapper over modern .NET 8 throw-helpers
Guard.ThrowIfNullOrWhiteSpace(value);              // → ArgumentException.ThrowIfNullOrWhiteSpace
Guard.ThrowIfNotEqual(value, other);               // → ArgumentOutOfRangeException.ThrowIfNotEqual
Guard.ThrowIfFalse(IsValidEmail(value), "Invalid email format.");

// EmailAddress.cs — factory + private ctor
[JsonConstructor] private EmailAddress(string value) => Value = value;
public static EmailAddress Create(string value)
{
    Guard.ThrowIfNullOrWhiteSpace(value);
    Guard.ThrowIfFalse(IsValidEmail(value), "Invalid email format.");
    return new EmailAddress(value.ToLower());     // normalize here too
}
```

Two things worth noticing:

- **The private ctor is what makes the factory mandatory.** Only `Create` (and JSON
  deserialization via `[JsonConstructor]`) can build an `EmailAddress`, so *no
  unvalidated email can exist*. If the ctor were public, `new EmailAddress("garbage")`
  would bypass everything.
- **Guard methods delegate to built-in .NET 8 throw-helpers**
  (`ArgumentException.ThrowIfNullOrWhiteSpace`, `ArgumentOutOfRangeException.ThrowIf*`).
  These were added to the BCL precisely so everyone stops hand-rolling `if (x == null)
  throw new ArgumentNullException(...)`. The `Guard` wrapper exists to give the codebase
  one named vocabulary and a place to add project-specific checks.
- Normalization lives *with* validation (`value.ToLower()` + `OrdinalIgnoreCase`
  comparisons elsewhere) — a rule split across two places is a rule enforced nowhere.

### 3. EF Fluent configuration — translate invariants into schema

```csharp
// PersonEntityConfiguration.cs
builder.Property(e => e.FirstName).IsRequired().HasMaxLength(MaxLength.C64);
builder.Property(e => e.Affiliation).IsRequired().HasMaxLength(MaxLength.C512);
builder.HasIndex(x => x.UserId).IsUnique();
builder.Property(e => e.UserId).IsRequired(false);   // optional made explicit, not implied
```

This is where the project's **`MaxLength` constants registry** pays off:

```csharp
// Blocks.Core/Constraints/MaxLength.cs
public const int C64 = 64;   // used as MaxLength.C64, never the literal 64
```

- One named place changes "emails are max 100" everywhere; literals `64`/`512` scattered
  across configs and DTOs are the failure mode this prevents.
- The named constant reads as a *decision* ("the C64 tier") rather than a magic number.
- Note the division of labor: **`required` in C# ⇒ `IsRequired()` in EF.** The domain
  states the rule; the config translates it to schema. (EF does infer some of this from
  nullability, but explicit is the project's style — see the deliberate
  `IsRequired(false)` on `UserId`.)
- Things C# *cannot* express — delete behavior, principal keys, discriminators, value
  converters (`HasEnumConversion()`), raw-SQL indexes — live only here. That's the
  previous note's argument for Fluent over annotations.

### 4. Aggregate root methods — rules enforced at the only place state changes

(From the DDD note.) Collections are private; mutation happens via domain methods on the
root like `AssignAuthor`, which validate *before* touching `_actors`. A rule like "every
author needs ≥1 contribution area" then has exactly one enforcement point — no handler
can construct an invalid child.

## The other popular ways (know them, recognize them in other codebases)

### Data Annotations — constraints as attributes on the entity

```csharp
[Required, MaxLength(64)] public string FirstName { get; init; }
```

- **Pros:** shortest syntax; the *same* attributes are reused by ASP.NET model validation
  and OpenAPI/Swagger on DTOs — genuinely earn their keep at the API edge.
- **Cons:** drags persistence concerns into the domain; no attributes for discriminators,
  converters, delete behavior; silently overridden by Fluent API (model layers: conventions
  → annotations → Fluent, last wins); magic-number lengths instead of the `MaxLength`
  registry. This project uses Fluent-first for entities; annotations appear on DTOs.

### FluentValidation — rules as separate validator classes

```csharp
public class CreatePersonValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(64);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(64);
    }
}
```

- The dominant library for **application-layer (input) validation**. Rules are data —
  composable, testable, localizable error messages, complex cross-property rules
  (`RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate)`).
- **Key distinction:** validates *commands/DTOs*, not entities. This project (following
  Clean Architecture) deliberately keeps validation split: shape/format rules on the
  command (FluentValidation would go here), invariants in the domain (VO factories), and
  the two must not drift — a common lint/review check.
- Pairs with a MediatR **pipeline behavior** (`ValidationBehavior<,>`) that runs all
  validators before a handler executes, throwing `ValidationException` → mapped to 400.

### `IValidatableObject` / `Validate(ValidationContext)` — self-validating entities

```csharp
public class Article : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext ctx) { ... }
}
```

EF Core calls it during `SaveChanges` validation (though that stage is being phased out).
Rarely the best choice now: couples validation to EF's save pipeline and mixes concerns
into the entity. Mentioned mainly because older codebases use it.

### Database CHECK constraints — the data's own bodyguard

```csharp
// in a config / migration
builder.ToTable(t => t.HasCheckConstraint("CK_Article_MaxAssets",
    "[MaxAssetCount] > 0 AND [MaxAssetCount] <= 10"));
```

- Survives *anything* — a bug in another service writing to the same DB, a manual SQL
  fix, a bad migration. Used when the data is more valuable than the app.
- Cost: rules live in two places (C# throws nicer errors; the DB is the backstop), and
  they're invisible to IntelliSense. This project puts its unique-index rule in a raw
  SQL annotation (`RawSql:Index`) — same spirit: push what matters into the schema.

### The "parse, don't validate" pattern — turn validation into types

The philosophical underpinning of `EmailAddress`:

> Don't check a string is a valid email and then keep passing it around as `string`
> (forcing every consumer to re-check or trust). **Validate once and let the type system
> carry the proof forward.** After construction, `EmailAddress` *is* the proof.

Every primitive that carries meaning in this codebase is a candidate: `EmailAddress`,
`FileExtension`, `AssetName`, `FileName`. The guard runs once at `Create`; downstream
code never re-validates because it never sees the unvalidated form.

## How the layers compose on one property (worked example)

`Person.Email` — watch each layer do its single job:

1. **Domain type:** `public required EmailAddress Email { get; init; }` — you *cannot*
   have a Person without an email, and it can't be swapped after creation.
2. **VO factory:** `EmailAddress.Create` — regex + whitespace guard + lowercase
   normalization. Invalid input *throws here*, not at save time.
3. **EF config:** `ComplexProperty(o => o.Email)` → column `nvarchar(64)`. Length
   mirrored at schema level so direct SQL writes can't overflow.
4. **(Future) API edge:** the DTO gets `[EmailAddress]`/FluentValidation for friendly
   400 responses *before* the domain ever throws.

Four enforcement points, zero duplication of logic — each layer translates the same
rule into its own vocabulary (compiler → exception → schema → HTTP 400).

## When to use which (decision guide)

- **Can the compiler catch it?** → `required`, non-nullable types, `init`. Always prefer.
- **Is it an invariant of the concept itself?** (a valid email) → value object + factory
  + Guard. Runs once, type carries the proof.
- **Is it about persistence shape?** (column length, index, FK) → EF Fluent config,
  referencing `MaxLength` constants.
- **Is it input shaping for a request?** (friendly error messages, optional-field rules)
  → FluentValidation on the command/DTO.
- **Would corrupt data be catastrophic even from outside the app?** → CHECK constraint
  as backstop.
- **Never** rely on EF's `SaveChanges` validation as the primary mechanism — it's late,
  partial (only catches what maps to schema), and the stage is deprecated in newer EF.

## Pattern to remember

> Restrictions are layered, not centralized: the **compiler** makes illegal states
> unrepresentable (`required`/`init`), the **domain** validates once at the door
> (Guard + VO factory, private ctor), **EF config** translates invariants into schema
> (`HasMaxLength(MaxLength.C64)`, indexes, delete behavior), and the **database** is the
> backstop. Popular alternatives — Data Annotations (entities+DTOs), FluentValidation
> (application layer), `IValidatableObject` (legacy), CHECK constraints (schema
> backstop) — slot into specific layers; prefer the earliest layer that can express the
> rule, because the earlier the failure, the cheaper it is.

---

# `ApplyConfigurationsFromAssembly` — discover all `IEntityTypeConfiguration<T>` at once

**Location in project:** `src/Articles/Services/Review/Review.Persistence/ReviewDbContext.cs`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // Tell EF to use all the configurations defined in EntityConfigurations folder
    modelBuilder.ApplyConfigurationsFromAssembly(this.GetType().Assembly);
}
```

## The alternative it replaces

Without it, every configuration must be wired by hand:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfiguration(new PersonEntityConfiguration());
    modelBuilder.ApplyConfiguration(new ArticleEntityConfiguration());
    modelBuilder.ApplyConfiguration(new JournalEntityConfiguration());
    // ... forgotten one = silently missing config (defaults everywhere)
}
```

The manual version's failure mode is nasty: forget a line and the app still works —
you just get `nvarchar(max)` columns, missing indexes, cascade-on-delete where you
wanted restrict, and the wrong table names. Nothing throws; the schema is quietly wrong.

## What the one-liner actually does

1. Scans the **assembly you pass** for every non-abstract class implementing
   `IEntityTypeConfiguration<T>` (the custom `EntityConfiguration<T>` base in this repo
   inherits that interface, so all its subclasses qualify).
2. Instantiates each with its parameterless constructor and calls `ApplyConfiguration`
   on it — equivalent to the manual list above, but discovered by reflection.
3. `this.GetType().Assembly` is `Review.Persistence` — the same assembly where the
   `EntityConfigurations/` folder lives. Config and DbContext travel together.

## Why `this.GetType().Assembly` and not a hard-coded name

It resolves to whatever assembly the **concrete** DbContext class belongs to — so it
stays correct if the assembly is renamed, and every service's DbContext can carry the
identical line while each still loading *its own* configurations:

| DbContext | `GetType().Assembly` | Loads |
|---|---|---|
| `ReviewDbContext` | `Review.Persistence` | `PersonEntityConfiguration`, `ArticleEntityConfiguration`, ... |
| `SubmissionDbContext` | `Submission.Persistence` | its own configs |

(Caveat: for a `partial`/inherited DbContext, `GetType()` returns the most-derived
runtime type; here `ReviewDbContext` is the concrete class so it's simply its home
assembly. If a shared base ever used `GetType()`, subclasses would pull configs from
*their* assemblies — occasionally useful, usually a surprise.)

## Fine print worth knowing

- **Needs a parameterless constructor** on each configuration class. All of this repo's
  `EntityConfiguration<T>` subclasses are plain `public class X : EntityConfiguration<Y>`,
  so they qualify. A config class requiring constructor arguments would throw at startup —
  for those, fall back to explicit `ApplyConfiguration(new X(args))`.
- **Overlapping config classes are an error.** Two `IEntityTypeConfiguration<Person>`
  classes in the scanned assembly → `InvalidOperationException: The entity type 'Person'
  has conflicting configuration...`. The scanner doesn't merge.
- **Fluent inline config still wins over scanned classes** in precedence (same as
  annotations vs Fluent): whatever runs later in `OnModelCreating` overrides.
- **Nothing is skipped by access level by default**: `internal` config classes are
  discovered too (non-public types included in the scan). An abstract class like
  `EntityConfiguration<T>` itself is skipped because it can't be instantiated.
- **Selectors exist for big solutions:** `ApplyConfigurationsFromAssembly(assembly,
  type => type.FullName!.Contains("Person"))` filters which configs load — handy when
  multiple bounded contexts share one assembly and must not see each other's configs.

## The startup-cost trade-off

Reflection over one assembly happens **once per context creation of the model** — and EF
caches the built model per context type, so effectively once per process. Cost is
negligible next to what it buys: adding a new entity = adding one configuration file,
zero edits to `ReviewDbContext`. The `EntityConfigurations/` folder is the single
registry; this line is what makes that folder self-applying.

## Pattern to remember

> `modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly)` = convention over
> configuration for the Persistence layer: any `IEntityTypeConfiguration<T>` (including
> custom bases like `EntityConfiguration<T>`) in the same assembly as the DbContext is
> discovered and applied automatically — no hand-maintained list, no silently-forgotten
> entity configs. Constraints: parameterless ctors, one config class per entity type.
> `GetType().Assembly` (not a literal name) keeps the line correct across renames and
> identical in every service.

---

# `FileServiceFactory` delegate — one constructor parameter, many file services

**Location in project:**
- Delegate: `src/Articles/Services/Review/Review.Application/FileStorage/FileServiceFactory.cs`
- Consumer (will use it): `src/Articles/Services/Review/Review.Application/Features/Articles/InitializeFromSubmission/ArticleApprovedForReviewEventHandler.cs` — currently injects `IFileService _fileService` (unused), factory still commented out
- Registration (finished project, course not there yet): `learn-microservices2-finished/src/Services/Review/Review.API/FileServiceFactoryRegistration.cs`
- Backing registrations: `Modules/FileStorage/FileStorage.MongoGridFS/FileStorageRegistration.cs`

## The business problem first

When an article is approved for review, the Review service must **copy the manuscript files out of Submission's storage into its own storage** (that's the `// todo - download the foles from submission and upload to review` comment in the handler). Same `IFileService` interface, but **two different configured backends**: the Submission service's Mongo GridFS bucket and Review's own bucket. The handler needs to pick between them **at runtime** — and DI alone can't express "give me the one pointing at Submission".

## What a delegate actually is

```csharp
public delegate IFileService FileServiceFactory(FileStorageType fileStorageType);
```

A delegate declaration is a **type declaration** — like `class` or `record`, it defines a new type whose *instances are methods*. Read it as: *"a `FileServiceFactory` is any method that takes a `FileStorageType` and returns an `IFileService`."*

- The **type** here is `FileServiceFactory` — it lives in a namespace, can be passed around, injected, stored in a variable.
- An **instance** is any compatible method — most commonly a lambda you write inline.
- The compiler turns each lambda into a method on a hidden closure class and wraps it in a delegate instance. Calling `_fileServiceFactory(FileStorageType.Submission)` simply invokes that method.

`Func<FileStorageType, IFileService>` would be technically equivalent — the named delegate is preferred because the *name documents intent* at the injection site (`FileServiceFactory _fileServiceFactory` reads as a concept; `Func<...>` reads as plumbing).

## The three pieces

### 1. The delegate + its parameter enum

```csharp
public enum FileStorageType { Review, Submission }

public delegate IFileService FileServiceFactory(FileStorageType fileStorageType);
```

The enum is a **closed set of well-known storages**. The consumer says *which* storage it wants by name; it never learns how that storage is implemented or configured.

### 2. Registration — a lambda that returns a lambda

(from the finished `Review.API/FileServiceFactoryRegistration.cs` — the composition root)

```csharp
services.AddScoped<FileServiceFactory>(serviceProvider => fileStorageType =>
{
    return fileStorageType switch
    {
        FileStorageType.Submission => serviceProvider.GetRequiredService<IFileService<SubmissionFileStorageOptions>>(),
        FileStorageType.Review     => serviceProvider.GetRequiredService<IFileService<MongoGridFsFileStorageOptions>>(),
        _ => throw new ApplicationException()
    };
});
```

**Two nested lambdas — this is the part to get straight:**

- **Outer lambda** `serviceProvider => ...` — this is the DI factory. The container calls it with the *current scope's* `IServiceProvider` when something requests a `FileServiceFactory`.
- **Inner lambda** `fileStorageType => ...` — this *is* the delegate instance being produced. It **closes over** `serviceProvider`, so whenever it's invoked later (mid-consumer), it resolves services **from the same scope the consumer lives in** — same lifetime, same scoped DbContext-family of dependencies.
- `AddScoped<FileServiceFactory>` — the delegate itself is a scoped service. Each HTTP request / MassTransit consume gets its own instance, wired to its own scope.

So the DI registration "service" here isn't a class — it's a *function*, produced per scope, that knows how to fetch the right file service.

### 3. Consumption — one parameter, any storage

```csharp
public class ArticleApprovedForReviewEventHandler(
    ...,
    IFileService _reviewFileService,        // "the default" — Review's own storage
    FileServiceFactory _fileServiceFactory  // "any storage, picked at runtime"
    ) : IConsumer<ArticleApprovedForReviewEvent>
{
    // in CreateAssets:
    var submissionFileService = _fileServiceFactory(FileStorageType.Submission);  // download from Submission
    var (fileStream, metadata) = await submissionFileService.DownloadAsync(assetDto.File.FileServerId, ct);

    fileMetadata = await _reviewFileService.UploadAsync(...);                     // upload into Review
}
```

(Exactly how the finished `ArticleApprovedForReviewConsumer` uses it. Note it injects **both**: plain `IFileService` for the service's own storage, the factory only for reaching *other* storages.)

## How DI constructs the consumer — step by step

MassTransit needs a new `ArticleApprovedForReviewEventHandler`:

1. It reflects the primary constructor and finds a parameter of type `FileServiceFactory`.
2. The container looks up its registration for `FileServiceFactory` → runs the **outer** lambda with the scope's provider → gets back a **delegate instance** (the inner lambda, holding a reference to that provider).
3. The delegate instance is passed to the constructor like any other dependency.
4. Later, handler code calls `_fileServiceFactory(FileStorageType.Submission)` → the inner lambda runs → `GetRequiredService<IFileService<SubmissionFileStorageOptions>>()` pulls the Submission-configured Mongo service from the same scope.

Nothing about Mongo, connection strings, or buckets ever leaks into the Application layer — the handler only knows an enum member and the interface.

## Why the generic `IFileService<TOptions>` exists (finished version)

One service can't register two implementations of plain `IFileService` — the container can't tell them apart. The finished contract adds:

```csharp
public interface IFileService<TFileStorageOptions> : IFileService
    where TFileStorageOptions : IFileStorageOptions;  // marker interface
```

The **generic type argument acts as the DI key**: `IFileService<SubmissionFileStorageOptions>` and `IFileService<MongoGridFsFileStorageOptions>` are two distinct service types, each backed by its own options (own connection string, own bucket) — yet both are usable through the shared non-generic `IFileService`. (This is the pre-.NET-8 idiom; modern .NET offers keyed services, `[FromKeyedServices]`, which solve the same problem with container support — worth recognizing both.)

## Why a delegate and not the alternatives

| Approach | Problem |
|---|---|
| Two `IFileService` constructor params | DI can't distinguish same-typed params; also doesn't scale with N storages |
| Inject `IServiceProvider` and resolve inline | Service-locator smell — any dependency reachable, untyped intent, untestable without the container |
| Abstract-factory **interface** (`IFileServiceFactory` + class) | Works fine, but one method doesn't need a class + registration ceremony; a delegate is the idiomatic minimal form |
| Keyed services (.NET 8) | Built-in alternative, but couples call sites to string/object keys; the delegate keeps selection logic in one typed place |

Delegate wins on the testability axis too: a unit test passes `_fileServiceFactory = _ => fakeFileService` — a lambda, no container setup.

## Course status ⚠️

- Current handler: injects `IFileService _fileService` (not yet used) — the factory parameter is **commented out**, waiting for the upcoming video.
- Current `IFileService` (`Modules/FileStorage/FileStorage.Contracts`) is the simple version: `UploadFileAsync / DownloadFileAsync / TryDeleteFileAsync`. The finished project's richer API (`FileMetadata`, `UploadAsync`, tag search) and the generic `IFileService<TOptions>` come later — don't jump ahead.
- The registration file shown above is from the **finished** project; in the current course code nothing registers `FileServiceFactory` yet — the delegate would resolve to nothing if injected today.

## Pattern to remember

> A delegate type = "a method shape as a first-class citizen." Register the delegate itself in DI (`AddScoped<TheDelegate>(provider => arg => ...)`) with the inner lambda closing over the scope's provider, inject the delegate type as one constructor parameter, and call sites choose between implementations at runtime by passing an argument — a lightweight, typed abstract factory with zero extra classes. The enum parameter names *which* storage; the registration owns *how* it's built.

---

# Marker interfaces — `IFileStorageOptions` and `IValueObject`

**Location in project:**
- Marker + its use as a generic constraint: `src/Articles/Modules/FileStorage/FileStorage.Contracts/IFileService.cs`
- Implementor: `src/Articles/Modules/FileStorage/FileStorage.MongoGridFS/MongoGridFsFileStorageOptions.cs`
- Used by the factory registration: `src/Articles/Services/Review/Review.API/FileServiceFactoryRegistration.cs`
- Second marker in this repo: `src/Articles/BuildingBlocks/Blocks.Domain/ValueObjects/IValueObject.cs`

## What a marker interface is

```csharp
public interface IFileStorageOptions; // marker interface — no members at all
```

An interface with **no methods, no properties — just a name**. A class "implementing" it inherits nothing to write; it only *declares a fact about itself*. C# 12's empty-body syntax (`interface IFileStorageOptions;`) makes them a one-liner.

```csharp
public class MongoGridFsFileStorageOptions : IFileStorageOptions   // opts into the concept
{
    [Required] public string ConnectionStringName { get; init; } = default!;
    [Required] public string DatabaseName { get; init; } = default!;
    public string BucketName { get; init; } = "files";
    ...
}
```

The class carries the data as before; the marker adds a **shared type identity** on top.

## Why the marker earns its keep here — the generic constraint

The real power isn't "tagging" — it's being the **boundary of a generic parameter**. The contracts file (with the course's own comment explaining the intent):

```csharp
// we need this version of the interface so we can include multiple file storages
// into a single microservice. TFileStorageOptions will allow that.
public interface IFileService<TFileStorageOptions> : IFileService
    where TFileStorageOptions : IFileStorageOptions;
```

`TFileStorageOptions` may be *any* type implementing the marker — and nothing else. That single `where` clause achieves three things:

1. **It's the generic counterpart of "same-typed params" in DI.** DI can't distinguish two registrations of plain `IFileService`, but `IFileService<SubmissionFileStorageOptions>` and `IFileService<MongoGridFsFileStorageOptions>` are *different closed generic types* — each gets its own registration, each is injectable by its exact type:
   ```csharp
   // FileServiceFactoryRegistration.cs — two distinct service types, both usable as IFileService
   FileStorageType.Submission => serviceProvider.GetRequiredService<IFileService<SubmissionFileStorageOptions>>(),
   FileStorageType.Review     => serviceProvider.GetRequiredService<IFileService<MongoGridFsFileStorageOptions>>(),
   ```
   This is exactly the mechanism the `FileServiceFactory` delegate (previous note) resolves through.
2. **It documents intent in the type signature.** `IFileService<MongoGridFsFileStorageOptions>` reads as "a file service for Mongo GridFS storage" without opening the class.
3. **It's compile-time safety for the *module boundary*.** A future `AzureBlobFileStorageOptions : IFileStorageOptions` (the finished project has it) slots into `IFileService<AzureBlobFileStorageOptions>` with zero contract changes. Someone's random `Person` class can never be passed as `TFileStorageOptions` — the compiler rejects it before it compiles.

So the marker is what makes the pattern scale: **options type = the DI key**, marker = the proof that the key is a valid storage configuration.

## The other marker in this repo: `IValueObject`

```csharp
// Blocks.Domain/ValueObjects/IValueObject.cs
public interface IValueObject;   // marker
```

Same idea, different role: it doesn't constrain a generic — it's a **shared base that unifies every value object** under one type. That buys the ability to write generic domain plumbing that accepts "any value object" (`where TVo : IValueObject` in a future base class, or reflection/scanning that finds all VOs by interface), and it documents the DDD concept in the type hierarchy. Same pattern, two uses: *constraint* and *classification*.

## The full benefit list (why not just "no interface at all")

| Without marker | With marker |
|---|---|
| Options classes are unrelated types — can't share a generic boundary | One `where T : IFileStorageOptions` admits exactly the right family |
| Can't write "works with any storage options" code | `IFileService<TOptions>` compiles for every storage, present and future |
| Intent is implicit (naming convention only) | `: IFileStorageOptions` states membership in the type system |
| Reflection/scan code has no hook | `typeof(IFileStorageOptions).IsAssignableFrom(t)` finds all of them |
| No shared identity for docs/DI keying/extension methods | Extension methods and DI helpers can target the marker |

## Trade-offs to know (senior-level nuance)

- **Marker interfaces are *not* free of criticism.** The classic objection ("empty interfaces are pseudo-tags, use attributes instead") applies to *classification* markers. But for **generic constraints** they're the right tool — attributes can't constrain a `T` in `where T : ...` (C# has no `where T : [Attribute]`).
- **A marker without a constraint does almost nothing.** The value here comes from the *pair*: marker + `where` clause. A class implementing the marker with no generic using it is pure documentation.
- **Naming convention:** `I`-prefix still applies (`IFileStorageOptions`), and the name should name the *concept*, not the mechanism (`IFileStorageOptions`, not `IMarker`).
- **Attributes vs markers, quick rule:** attributes = metadata *about* a type (read reflectively, e.g. `[Required]`); marker interface = type identity *of* a type (usable in signatures, constraints, DI keys). When you need the type system to care — constraints, pattern matching, injection — use the interface.

## Pattern to remember

> Marker interface = an empty interface whose only job is to give a family of types a shared name the compiler can see. Its two real uses: **as a generic constraint** (`where T : IMarker` — turns "any type" into "any type in this family", which here is what lets one microservice host multiple file storages keyed by their options class) and **as a classification tag** (unifies disparate types under one interface for scanning/plumbing). Pair it with a generic parameter and it becomes the DI key; on its own it's just documentation.

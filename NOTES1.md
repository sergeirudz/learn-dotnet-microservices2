# C# / .NET Notes
- When adding notes. Please try to reference files with locations      

## `implicit operator` (user-defined conversions)

**File reference:** `src/Articles/BuildingBlocks/Blocks.Domain/ValueObjects/StringValueObject.cs`

The line that prompted this note:

```csharp
// Cast StringValueObject into a string
public static implicit operator string(StringValueObject @object) => @object.Value;
```

### What it means

It's a **user-defined conversion**. It tells the C# compiler: *"whenever you see a
`StringValueObject` in a place where a `string` is expected, automatically convert it by
running this code."*

So this works without any cast syntax:

```csharp
FileName name = ...;
string s = name;          // implicit: compiler inserts the conversion for you
Console.WriteLine(name);  // also converts to string
```

The two keywords:

- **`operator`** — you're defining a custom conversion operator (conversions are a kind of
  operator in C#, like `+` or `==`).
- **`implicit`** — the conversion happens automatically, no cast needed. Its sibling is
  **`explicit`**, which *requires* a cast.

### Reading the signature

```csharp
public static implicit operator string(StringValueObject @object)
//                          ^^^^^^ ^^^^^^^^^^^^^^^^^^^^^^^
//                    convert TO   convert FROM (the parameter)
```

- The type after `operator` (`string`) = what you're converting **to**.
- The parameter type (`StringValueObject`) = what you're converting **from**.
- It must be `static` (it's a type-level rule, not per-instance) and `public`.
- `@object` is just the parameter name — the `@` prefix escapes it because `object` is a C#
  keyword, so you can't use it bare as an identifier.

### `implicit` vs `explicit` — the design choice

```csharp
public static explicit operator FileName(string value) => new FileName(value);
// usage — cast REQUIRED:
FileName n = (FileName)someString;
```

Rule of thumb:

| Use          | When                                                   | Why                                  |
|--------------|--------------------------------------------------------|--------------------------------------|
| **`implicit`** | Conversion is **always safe**, never loses data, can't fail | No surprise for the caller           |
| **`explicit`** | Conversion **can fail or lose information**            | Forces the caller to acknowledge risk |

In `StringValueObject`, `StringValueObject -> string` is `implicit` because it's always safe —
every `StringValueObject` *has* a string inside, so nothing can go wrong.

The reverse direction (`string -> FileName`) is what you'd typically make `explicit` (or better,
a factory method like `FromAssetType`) — because an arbitrary string might be *invalid* (too
long, wrong extension, empty). You don't want that silently slipping through, which is the whole
point of a value object.

### Why value objects do this

A **value object** wraps a primitive (`string`, `int`) to give it domain meaning and validation.
`FileName` is more expressive than a bare `string` — the type itself documents intent and
enforces rules. But you still need to *get the raw string out* to save it, send it over gRPC,
etc. The `implicit operator string` makes the value object behave nicely at those boundaries: you
can pass it to any method expecting a `string` without noise like `.Value` everywhere.

### Bugs spotted in the current code (to fix later)

`src/Articles/Services/Submission/Submission.Domain/ValueObjects/FileName.cs` won't compile — the
class is `FileName` but its constructor and factory are named `AssetName`, and it references an
`AssetType` that isn't there:

```csharp
public class FileName
{
    private AssetName(string value) { ... }   // constructor name must match class: FileName
    public static AssetName FromAssetType(AssetType assetType) { ... }  // AssetName/AssetType
}
```

Looks like a copy-paste from `AssetName.cs`.

Also, in `StringValueObject.cs`:

- `GetHasCode()` is a typo for `GetHashCode()` (so it isn't actually overriding the base method).
- `Equals(StringValueObject? other)` will throw if `other` is null — use `other?.Value`.

## Global usings and global aliases

**File reference:** `src/Articles/Services/Submission/Submission.Application/GlobalUsing.cs`

The file that motivated this note:

```csharp
// Global aliases
global using AssetTypeDefinitionRepository = Blocks.EntityFrameworkCore.CachedRepository<
    Submission.Persistence.SubmissionDbContext,
    Submission.Domain.Entities.AssetTypeDefinition,
    Articles.Abstractions.Enums.AssetType>;
`````
``
### What `global using` does

A normal `using` at the top of one file only applies to that **one file**. A `global using`
applies to **every file in the project** (compilation unit). So instead of writing
`using MediatR;` in 30 handler files, you write it once in `GlobalUsing.cs` and every file in
that project sees `MediatR`, `FluentValidation`, your domain entities, etc.

```csharp
// Third-party libraries
global using MediatR;
global using FluentValidation;

// Internal libraries
global using Blocks.MediatR;
global using Articles.Abstractions;

// Domain
global using Submission.Domain.Entities;
```

### Benefits

- **Less boilerplate** — the same `using` block isn't copy-pasted into every file.
- **Cleaner files** — a handler opens straight into its own code instead of 15 lines of imports.
- **One place to change** — add/remove a shared namespace once, not in every file.
- **A project-wide contract** — everything a feature commonly needs (MediatR, shared
  abstractions, your entities) is available by default.

### Drawbacks (real, not just theoretical)

- **Less obvious where a type comes from** — you can't glance at the top of a file to see its
  dependencies; you may have to search the project.
- **Name collisions** — if two imported namespaces define the same type name (the note below
  mentions `ValidationMessages`), the compiler can't tell which one you mean.

### What a `global using` *alias* does

A global using can bind **one name to a whole generic type**, creating a project-wide alias.
In `GlobalUsing.cs`:

```csharp
global using AssetTypeDefinitionRepository = Blocks.EntityFrameworkCore.CachedRepository<
    Submission.Persistence.SubmissionDbContext,
    Submission.Domain.Entities.AssetTypeDefinition,
    Articles.Abstractions.Enums.AssetType>;
```

This says: *wherever I write `AssetTypeDefinitionRepository`, expand it to
`CachedRepository<SubmissionDbContext, AssetTypeDefinition, AssetType>`.*

### Why the alias was used here (not a class)

`AssetTypeDefinitionRepository.cs` originally planned to be a **subclass**:

```csharp
// The commented-out original — a real class:
public class AssetTypeDefinitionRepository(SubmissionDbContext dbContext, IMemoryCache cache)
    : CachedRepository<SubmissionDbContext, AssetTypeDefinition, AssetType>(dbContext, cache);
```

But that's a whole class in a whole file just to pin down three type arguments. Since the
`CachedRepository` base already has everything (it's not `abstract` in a way that needs a body),
a **type alias** expresses the same thing with zero new code:

```csharp
// The alias — no class, no constructor, no file needed beyond the global using:
global using AssetTypeDefinitionRepository = Blocks.EntityFrameworkCore.CachedRepository<...>;
```

The handler then injects it directly as if it were a named type:

```csharp
public class UploadManuscriptFileCommandHandler(
    ArticleRepository _articleRepository,
    AssetTypeDefinitionRepository _assetTypeRepository)
    : IRequestHandler<UploadManuscriptFileCommand, IdResponse>
{
    ...
}
```

### When to reach for an alias vs. a class

| You want…                       | Use                       |
|---------------------------------|---------------------------|
| A name for a fully-specified generic type, nothing extra | **global using alias** |
| Extra methods / overrides / validation on top of the base | **a subclass**           |
| To register it in DI by a distinct type/lifetime           | **a class**               |

**Caveat about DI:** a `global using` alias is **purely a compile-time name** — it produces *no
new type*, so you register *the base* `CachedRepository<...>` in DI, not `AssetTypeDefinitionRepository`.
`AssetTypeDefinitionRepository` only exists as source text; at runtime there is exactly one
`CachedRepository<SubmissionDbContext, AssetTypeDefinition, AssetType>`. If you later need this
repository to have its own behavior or its own DI registration, promote the alias to a real
`public class` and delete the alias line.

## Extension methods: reading an options section from appsettings.json

**File reference (how it's used):**
`src/Articles/Modules/FileStorage/FileStorage.MongoGridFS/FileStorageRegistration.cs`

**File reference (where the helpers live):**
`src/Articles/BuildingBlocks/Blocks.Core/ConfigurationExtensions.cs`

The lines that prompted this note:

```csharp
// In FileStorageRegistration.cs
public static IServiceCollection AddMongoFileStorage(this IServiceCollection services, IConfiguration configuration)
{
    services.AddAndValidateOptions<MongoGridFsFileStorageOptions>(configuration);

    var options = configuration.GetSectionByTypeName<MongoGridFsFileStorageOptions>();
    ...
}
```

### What these two lines do (and why there are two)

1. **`AddAndValidateOptions<T>(configuration)`** registers `T` in the .NET **Options** system.
   It binds the `appsettings.json` section whose name matches the class name, then runs
   `[DataAnnotations]` validation and fails at startup if required values are missing. This is
   the line that lets you later inject `IOptions<T>` / `IOptionsSnapshot<T>` anywhere.

2. **`GetSectionByTypeName<T>()`** just reads the same section back out into a plain `T` object
   right now, so the registration method itself can use the values immediately (e.g. to build a
   `MongoClient`, a database handle, a `GridFSBucket`).

The key idea: **the section name in appsettings.json must equal the class name.** Both helpers
use `typeof(T).Name` as the section key:

```csharp
public static T GetSectionByTypeName<T>(this IConfiguration configuration)
{
    var sectionName = typeof(T).Name;
    var section = configuration.GetSection(sectionName).Get<T>()!;
    return Guard.AgainstNull(section, sectionName);
}
```

So a class `MongoGridFsFileStorageOptions` maps to a JSON section literally named
`MongoGridFsFileStorageOptions`:

```json
{
  "MongoGridFsFileStorageOptions": {
    "ConnectionStringName": "MongoDb",
    "DatabaseName": "FileStorage",
    "BucketName": "manuscripts",
    "ChunkSizeBytes": 261120
  }
}
```

(Note: `ChunkSizeBytes`, `BucketName`, etc. are just example property names pulled from how the
options are used in `FileStorageRegistration.cs` — match yours to the actual `T` class.)

### What an "extension method" actually is

Both helpers are **extension methods** — a C# feature that lets you call a method *as if it were
defined on an existing type you don't own*, like `IConfiguration` or `IServiceCollection`.

The syntax rules that make a method an extension method:

```csharp
public static class ConfigurationExtensions            // (1) must live in a static class
{
    public static T GetSectionByTypeName<T>(this IConfiguration configuration)
    //     ^^^^^^^                             ^^^^   ^^^^^^^^^^^^^^^^^^^^^^
    //   must be static                       KEYWORD   the type being extended
    {
        ...
    }
}
```

The three ingredients:

1. The method is `static` and sits in a `static` class.
2. The **first parameter** is prefixed with the `this` keyword.
3. That first parameter's type (`IConfiguration`) is **the type being extended**.

Then you call it as if it were a real instance method:

```csharp
var options = configuration.GetSectionByTypeName<MongoGridFsFileStorageOptions>();
//               ^^^^^^^^^^^^^ configuration is an IConfiguration — but the method isn't on it;
//                              the `this` keyword wires it up so the compiler accepts this call.
```

At runtime nothing special happens — `GetSectionByTypeName(configuration)` is just a static call.
The `this` keyword is *compile-time sugar*: it tells the compiler "let people write
`configuration.GetSectionByTypeName(...)` and treat the receiver as the first argument."

### Why this pattern exists in the project

.NET can already bind config into a typed object (`configuration.GetSection("Name").Get<T>()`),
and it already has Options + `AddOptions<T>().Bind(...)`. The project wraps these into two tiny
helpers to encode its **naming convention** — *section key = class name* — and its **fail-fast**
guarantees (missing section or missing required values throw at startup instead of half-way
through a request).

So the shorthand `configuration.GetSectionByTypeName<T>()` is really the project's answer to
"don't repeat `GetSection(typeof(T).Name)` and a null guard in every registration file."

### Generic methods — the `<T>` angle

Both helpers are **generic methods** (`<T>` / `<TOptions>`). The `<T>` is a *type parameter* passed
at the call site: `GetSectionByTypeName<MongoGridFsFileStorageOptions>()` makes `T` mean
`MongoGridFsFileStorageOptions` for that call. The method body then uses `typeof(T)` to turn the
type *back into a name at runtime* — which is the whole trick behind "section key = class name".

The `where TOptions : class` constraint on `AddAndValidateOptions` just guarantees `TOptions` is a
reference type, which `AddOptions<TOptions>()` requires.

## Private collection field + public `IReadOnlyList` (encapsulating `_userRoles`)

**File reference:** `src/Articles/Services/Auth/Auth.Domain/Users/Users.cs`
**Related:** `src/Articles/Services/Auth/Auth.Domain/Users/UserRole.cs`

The lines that prompted this note:

```csharp
private List<UserRole> _userRoles = new List<UserRole>();
public virtual IReadOnlyList<UserRole> UserRoles => _userRoles;
```

### The problem with the "obvious" version

The tempting shortcut is a single public list:

```csharp
// DON'T do this
public List<UserRole> UserRoles { get; set; } = new();
```

`List<T>` hands out its **mutating API**: `Add`, `Remove`, `Clear`, `Insert`, indexer assignment
(`user.UserRoles[0] = someRole`). So *any* caller anywhere in the codebase can do:

```csharp
user.UserRoles.Add(new UserRole { UserId = 5, RoleId = 3 });  // no rules applied at all
user.UserRoles.Clear();                                        // deletes a person's roles
```

The entity has no say in it. `UserRole` carries domain data — `StartDate`, `ExpiringDate` — and the
whole point of the domain model is that **role changes go through the entity's behavior**, not
through whatever a caller felt like doing to a collection.

### What the two lines buy you

- **The field is `private`** — outside code cannot touch `_userRoles` directly. The collection
  becomes internal state of `Users`.
- **The property exposes an `IReadOnlyList<UserRole>`** — an *interface* that has no `Add`, no
  `Remove`, no settable indexer. Callers can `foreach`, `Count`, `.Any(...)`, index *read* — but the
  compiler refuses to compile a mutation through it.
- **Mutation is funneled into behaviors** (methods on `Users`, e.g. `AddRole`, `ChangeRole`,
  `RevokeRole`). That's where invariants live: set/default `StartDate`, forbid duplicate roles,
  enforce that only one role is active at a time, stamp `ExpiringDate` when replacing, raise a
  domain event, and so on.

So the shape of the rule is: **the collection is readable by everyone, writable only by the
aggregate itself.** Encapsulation here isn't secrecy for its own sake — it's the enforcement point
for the rules that make `UserRole` meaningful.

One more practical benefit: when role assignment later needs to send an integration event
(`UserRoleAssigned`, `RolesChanged` for the token), there's exactly one place to put that — the
behavior — instead of hunting every `.Add()` scattered across handlers.

### No copy is being made — worth knowing

```csharp
public virtual IReadOnlyList<UserRole> UserRoles => _userRoles;
```

The property returns the **same `List<UserRole>` object**, just typed as a narrower interface. It's
a *view*, not a snapshot. So a caller who reads `UserRoles` before a role change sees the new role
afterwards through that same reference. That's normally what you want; if you ever need a frozen
snapshot, you'd return `_userRoles.ToList()` or `AsReadOnly()` — at the cost of an allocation per
read.

### Does `private` break EF Core?

No — this is a supported and common pattern. EF Core has a **backing field convention**: for a
navigation property named `UserRoles`, it looks for a field named `_userRoles` (also `_UserRoles`,
`m_userRoles`, or a field whose type is compatible and name matches case-insensitively). It finds
the private field, uses it for materialization and change tracking, and the property is just the
read-only window. If the convention ever fails to match, you can state it explicitly in
`OnModelCreating` with `.UsePropertyAccessMode(PropertyAccessMode.Field)` or by naming the field in
the mapping — but the naming (`_userRoles` for `UserRoles`) is chosen to match the convention.

Small nit: the field is never reassigned, so it can be `private readonly List<UserRole> _userRoles = [];`
— and since it's `IReadOnlyList`, a collection expression (`[]`) works for the initializer in
modern C#.

### The general pattern

| Expose                                     | When                                              |
|--------------------------------------------|---------------------------------------------------|
| `private List<T> _items` + `IReadOnlyList<T>` | The entity owns the collection and must enforce rules on change |
| `public List<T>` (get-only)                | Almost never — still hand out `Add`/`Clear` |
| `public IReadOnlyList<T>` with no behaviors | Fine for genuinely read-only projections, but then nothing can legitimately change it either |

Note the `virtual` on `UserRoles` — that's for EF Core lazy-loading proxies; EF overrides the
property to trigger a load if the collection wasn't included in the query.

## `Guard.NotFound<T>` — null-guard that throws a 404

**File references:**
- `src/Articles/BuildingBlocks/Blocks.Core/Guard.cs` — the method
- `src/Articles/BuildingBlocks/Blocks.Exceptions/NotFoundException.cs`, `HttpException.cs` — what it throws
- `src/Articles/Services/Auth/Auth.API/Features/Users/Login/LoginEndpoint.cs:26,29` — the usage that prompted this note

The lines that prompted this note:

```csharp
var person = Guard.NotFound(
    await _personRepository.GetByEmailAsync(command.Email, ct)
);
var user = Guard.NotFound(person.User);
```

### The method itself

```csharp
public static T NotFound<T>(T? value) where T : class
    => value ?? throw new NotFoundException($"{typeof(T).Name} not found");
```

Read it piece by piece:

| Piece | Meaning |
|---|---|
| `static` | No instance state — it's called as `Guard.NotFound(...)`, never `new Guard().NotFound(...)` |
| `T NotFound<T>` | **Generic method**: `T` is a *type parameter* filled in at the call site. The `T` after `NotFound` is the **return type** |
| `(T? value)` | Parameter is **nullable `T?`** — the caller may hand you `null`, and that's the whole point |
| `where T : class` | Constraint: `T` must be a **reference type** (class/interface). Needed so `T?` means "nullable reference" and `??` is allowed. A value type (`int`) can't be `null` without `Nullable<T>`, and `typeof(T).Name` reporting "Int32 not found" is meaningless anyway |
| `=>` | Expression-bodied member — the whole body is one expression |
| `value ?? throw ...` | **Null-coalescing with a throw expression**: if `value` is non-null, return it; otherwise evaluate the right side, which throws |
| `typeof(T).Name` | The *runtime* name of the type argument — for `Guard.NotFound(person.User)` with `T = User` this yields `"User"` |

### The two call sites, one method

**Site 1 — a repository query that may find nothing:**

```csharp
var person = Guard.NotFound(
    await _personRepository.GetByEmailAsync(command.Email, ct)
);
```

`GetByEmailAsync` is declared `Task<Person?>` — it returns `null` when no person has that email. Here the type parameter is **inferred** as `Person`, so `person` comes back as **`Person` (non-nullable)**, not `Person?`. If the email doesn't exist, this throws `NotFoundException` → HTTP 404 instead of continuing with a null and blowing up later.

**Site 2 — a navigation property that may not be loaded:**

```csharp
var user = Guard.NotFound(person.User);
```

`person` is already non-null at this point, so `person.User` is a safe dereference. But `User` is declared as an **optional navigation**:

```csharp
// src/Articles/Services/Auth/Auth.Domain/Persons/Person.cs
public int? UserId { get; set; }
public User? User { get; set; }
```

A `Person` can exist without a `User` account (e.g. an invited author who never registered), so the property is `User?` and reads as `null` in that case. Here `T` is inferred as `User` and the method returns `User`.

Note it's the **property access**, not the repository, that would NRE — that's exactly what the guard prevents. (The repository does `Include(p => p.User)` in `PersonRepository.Query()`, so when a user *does* exist, the joined entity is loaded rather than being a null lazy-load placeholder.)

### What you get: the return type is non-nullable

This is the real payoff. Compare:

```csharp
// Before — the caller still has to reason about null afterwards
Person? person = await _personRepository.GetByEmailAsync(command.Email, ct);
if (person is null)
    throw new NotFoundException("Person not found");
// the compiler still tracks `person` as maybe-null inside nested lambdas etc.

// After — the type system is told the check already happened
var person = Guard.NotFound(await _personRepository.GetByEmailAsync(command.Email, ct));
// `person` is Person, not Person? — no `!`, no warning at `person.User`
```

`Guard.NotFound` is declared to return `T`, and `throw` is a valid expression on the right of `??` — so the compiler's **nullable flow analysis** sees the method as "either throws or gives you a non-null `T`". It centralizes the check-and-throw in one place instead of an `if (x is null) throw` block repeated at every read.

### Where the exception goes

`NotFoundException` isn't just any exception — it derives from `HttpException`:

```csharp
public class NotFoundException : HttpException          // 404
public class BadRequestException  : HttpException       // 400

public class HttpException : Exception
{
    public HttpException(HttpStatusCode statusCode, string message) : base(...) { ... }
    public HttpStatusCode HttpStatusCode { get; }
}
```

So the exception carries a **status code** as data. The plan in the project is one piece of middleware translating `HttpException.HttpStatusCode` into the HTTP response — which is why endpoints can just `throw` domain-ish failures (`NotFoundException`, `BadRequestException`) and let a single handler turn them into 404/400, instead of every endpoint building its own result object. Note: as of this commit that middleware isn't wired into `Auth.API/Program.cs` yet, so today the exception would surface as a 500.

### Distinguish the two families in `Guard`

`Guard.cs` mixes guards with **different meanings**, despite the name:

| Method | Throws | Meaning |
|---|---|---|
| `Guard.NotFound(value)` | `NotFoundException` (404) | **Expected runtime outcome** — the resource legitimately isn't there. Part of the API contract |
| `Guard.AgainstNull(value, name)` | `ArgumentNullException` (500) | **Programmer error** — a required argument wasn't supplied. A bug, not a user situation |
| `Guard.ThrowIfNullOrWhiteSpace(value)` | `ArgumentException` (500) | Same — bad input to a constructor/factory (`EmailAddress`, `FileExtension`) |
| `Guard.ThrowIfFalse(cond, msg)` | `ArgumentException` (500) | Invariant violation in a factory |

That's why `Guard.NotFound` exists as a separate method: a missing entity is a *different kind of failure* than a missing argument, and the caller deserves a different status code.

Side note on the current shape: `ThrowIfFalse` and `IsNullOrEmpty` are marked `this` — they're **extension methods**, so they're called as `condition.ThrowIfFalse("...")`, not `Guard.ThrowIfFalse(...)`. Mixing static helpers and extension methods in one class named `Guard` is a little confusing; the finished project separates the extension variant into its own `GuardExtensions` class.

### The finished project's follow-up direction

The finished code keeps `Guard.NotFound` and then builds on top of it to **remove the call site entirely**:

```csharp
// finished: BuildingBlocks/Blocks.EntityFrameworkCore/Extensions/RepositoryExtensions.cs
public static async Task<TEntity> FindByIdOrThrowAsync<TEntity, TContext>(this RepositoryBase<TContext, TEntity> repository, int id)
    where TContext : DbContext
    where TEntity : class, IEntity<int>
    => Guard.NotFound(await repository.FindByIdAsync(id));

public static async Task<TEntity> SingleOrThrowAsync<TEntity>(this IQueryable<TEntity> source, Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    => Guard.NotFound(await source.SingleOrDefaultAsync(predicate, ct));
```

(Your current `RepositoryExtensions.cs` at `src/Articles/BuildingBlocks/Blocks.EntityFramework/RepositoryExtensions.cs` does the same thing but with an inline `if (is null) throw` instead of delegating to `Guard.NotFound` — the finished version is the same behavior, deduplicated.)

And a second, shorter spelling for arbitrary values:

```csharp
// finished: BuildingBlocks/Blocks.Core/GuardExtensions.cs
public static T OrThrowNotFound<T>(this T? value, string? message = null) where T : class
    => value ?? throw new NotFoundException(message ?? $"{typeof(T).Name} not found");

// usage
var user = person.User.OrThrowNotFound();
```

Same semantics, but reads as a chain off the value instead of wrapping it. Two spellings exist on purpose, so you can compare styles: `Guard.NotFound(x)` (wrap) vs `x.OrThrowNotFound()` (chain).

### Quick reference: `??` + `throw`

A **throw expression** is C# 7's way of allowing `throw` where an *expression* is expected (the right side of `??`, `?:`, `=>`). Combined with `??` it produces the canonical null-guard idiom:

```csharp
var value = maybeNull ?? throw new NotFoundException("...");
```

Values (`??`), methods (`=>`), generics (`<T>` with `where T : class`), and throw expressions stack up into one line — it's dense, but each piece is doing one job.

## Redis OM `[Document]` + indexing (vs. EF Core)

**File references (the model):**
- `src/Articles/Services/Journals/Journals.Domain/Journals/Journal.cs` — `[Document(StorageType = StorageType.Json, Prefixes = new[] { nameof(Journal) })]`, `[Indexed]`, `[Indexed(JsonPath = "$.Name")]`
- `src/Articles/BuildingBlocks/Blocks.Redis/Entity.cs` — `[RedisIdField] [Indexed] public int Id`
- `src/Articles/Services/Journals/Journals.Domain/Journals/Section.cs`, `.../ValueObjects/SectionEditor.cs`

**File references (the infrastructure):**
- `src/Articles/BuildingBlocks/Blocks.Redis/Repository.cs` — `InsertAsync`/`UpdateAsync`/`ReplaceAsync`
- `src/Articles/Services/Journals/Journals.Persistence/JournalDbContext.cs` — holds `RedisConnectionProvider`
- finished: `src/Services/Journals/Journals.API/AppBuilderExtensions.cs` — calls `provider.Connection.CreateIndex(typeof(Editor))` / `(typeof(Journal))` on startup

### `[Document]` — the Redis OM "table" declaration

Redis OM needs to know two things EF Core would otherwise infer: **how** to serialize and **under what key**.

```csharp
[Document(StorageType = StorageType.Json, Prefixes = new[] { nameof(Journal) })]
```

| Argument | Meaning |
|---|---|
| `StorageType = Json` | Store as a JSON document (RedisJSON), not a flat Hash. Needed because `Journal` has a nested `List<Section>` — hashes can't nest. |
| `Prefixes = { "Journal" }` | Key prefix → keys become `Journal:1`, `Journal:2`. Combined with `[RedisIdField] Id` from `Entity`. |

`nameof(Journal)` instead of the literal `"Journal"` = refactor safety. `Prefixes` is an array because one type can live under several prefixes.

### What indexing actually is

A **secondary index** (RediSearch) — an inverted lookup from a value back to the documents holding it:

```
Abbreviation "JAMA" → [Journal:1, Journal:7]
Sections[].Name "Cardiology" → [Journal:1]
```

Plain Redis can only fetch a known key (`GET Journal:1`, O(1)). Querying *by value* (`Where(j => j.Abbreviation == ...)`) without an index means scanning every key, O(N). The index trades extra storage + write cost for fast reads.

**`[Indexed]` alone does nothing.** It's only a *declaration*. The index doesn't exist until `CreateIndex` runs (see the finished `AppBuilderExtensions.cs`). Two halves of one thing:

| Piece | Where | Role |
|---|---|---|
| `[Indexed]`, `JsonPath` | domain class | declares **what** is searchable (the schema) |
| `CreateIndex(typeof(...))` | app startup | actually **builds** it inside Redis |

Notes:
- The index lives **outside** the document — your JSON at `Journal:1` is untouched, and Redis maintains the index on every insert/update/delete.
- **Non-indexed fields are still stored and readable** (`Description`, `ISSN`, `ChiefEditorId` come back from `FindById`) — they just can't be filtered on. Redis OM throws if you query a non-indexed field.
- `[Indexed(JsonPath = "$.Name")]` on `Sections` indexes a property *nested inside* array elements, since those aren't top-level document fields.
- Because the index is rebuilt per-document, `Repository.ReplaceAsync` (`src/Articles/BuildingBlocks/Blocks.Redis/Repository.cs`) does delete+insert — a workaround for Redis OM not updating nested collections in place.
- In the current project nothing calls `CreateIndex` yet, so every `[Indexed]` attribute (`Journal`, `Section`, `SectionEditor`) is currently **inert**.

### The EF Core analogy

| EF Core | Redis OM |
|---|---|
| `modelBuilder.Entity<T>().HasIndex(x => x.Abbreviation)` | `[Indexed] public string Abbreviation` |
| `dotnet ef migrations add` + `database update` | `provider.Connection.CreateIndex(typeof(T))` at startup |
| Schema inferred from conventions | Requires explicit `[Document]` on the root type |

Same idea on both sides: **an index is a declaration until something applies it.** For SQL that's a migration you run deliberately and version; for Redis it's the `CreateIndex` call on every boot (idempotent — creates only if missing).

Also note `[Document]` and `Entity` do two different jobs: `[Document]` is the Redis mapping, while `Journal : Entity` is only what satisfies `Repository<T> where T : Entity` (and supplies the `Id` key).

## gRPC: how the code-first service works

**File references:**
- Contract: `src/Articles/BuildingBlocks/Articles.Grpc.Contracts/Auth/PersonContracts.cs` (`IPersonService`, `PersonInfo`, the request/response DTOs)
- The unused "standard" equivalent: `.../PersonContracts.proto`
- Implementation: `src/Articles/Services/Auth/Auth.API/Features/Persons/PersonGrpcService.cs`
- Registration: `src/Articles/Services/Auth/Auth.API/DependencyInjection.cs:39` (`AddCodeFirstGrpc`), `Program.cs:33` (`MapGrpcService<PersonGrpcService>()`)
- Mapping: `src/Articles/Services/Auth/Auth.Domain/Persons/GrpcTypeAdapterConfig.cs`
- Client side (finished): `finished/src/BuildingBlocks/Blocks.AspNetCore/Grpc/GrpcClientRegistrationExtensions.cs`

### The big picture

This is **code-first gRPC** via `protobuf-net.Grpc` — *not* the classic `.proto` + `protoc` codegen flow. The `.proto` file sitting next to the contract is a leftover reference for "what this would look like in standard gRPC"; nothing in the build uses it (the `GrpcTypeAdapterConfig` comment literally says *"this is for standard GRPC, we can safely remove it"*).

Code-first flips the workflow around:

| | Standard gRPC | Code-first (this project) |
|---|---|---|
| Source of truth | `.proto` file | A C# `interface` |
| Codegen step | `protoc` → generated C# stubs | none |
| The contract type | generated `Foo.BarClient` / `FooServiceBase` | your own `IPersonService` |
| Both sides share | the `.proto` | the same interface + DTOs (one project reference) |

The payoff is the last row: because the contract is a plain C# interface in a shared building block, **the server and every client reference the same `IPersonService`**, and a remote call looks like a local method call. That's the whole reason this is worth the setup.

### The four pieces, in order

**1. The contract** (`Articles.Grpc.Contracts/Auth/PersonContracts.cs`)

```csharp
[ServiceContract]
public interface IPersonService
{
    [OperationContract]
    ValueTask<GetPersonResponse> GetPersonByIdAsync(GetPersonRequest request, CallContext context = default);
    ...
}
```

- This is a **WCF-style** set of attributes (`System.ServiceModel`) — protobuf-net.Grpc reuses those names on purpose, so the shape is familiar.
- `[ProtoContract]` on the DTOs, and a **`[ProtoMember(n)]` on every property**. That number is the wire identity — it's what actually travels. Property *names* don't. This is why you can rename a DTO property without breaking clients, but you must **never reuse or renumber** a `ProtoMember` (that silently corrupts data between versions).
- `CallContext context = default` — the per-call handle. It carries the **cancellation token** and lets you read/write **headers/metadata**. This is gRPC's equivalent of `HttpContext`, and it's how `context.CancellationToken` reaches your repository call.
- `CreatePersonRequest : IPersonCreationInfo` — a nice trick worth noticing: the DTO *structurally satisfies the domain interface* (`src/Articles/BuildingBlocks/Articles.Abstractions/IPersonCreationInfo.cs`), so `Person.Create(request)` accepts the wire DTO directly. No DTO→domain mapping object needed on the write path.

**2. Server registration** (`Auth.API/DependencyInjection.cs`)

```csharp
services.AddCodeFirstGrpc(options =>
{
    options.ResponseCompressionLevel = CompressionLevel.Fastest;
    options.EnableDetailedErrors = true;
});
```

- `AddCodeFirstGrpc` comes from `protobuf-net.Grpc.AspNetCore` and bolts code-first binding onto the standard `Grpc.AspNetCore.Server` stack. Underneath, it's still normal gRPC.
- `EnableDetailedErrors = true` is a **development crutch**: it sends the server's exception message to the client instead of a bare "unknown error". Useful now, but it leaks internals — production would turn it off and map errors properly (see the interceptor section below).
- Note it must be registered **before** `app.Build()` — it's all `IServiceCollection` wiring.

**3. Endpoint mapping** (`Auth.API/Program.cs:33`)

```csharp
app.MapGrpcService<PersonGrpcService>(); // We will expose this GRPC service to external clients
```

- This publishes the implementation as an endpoint. gRPC rides on **HTTP/2**, so Kestrel is the transport; the route is roughly `/{ServiceName}/{MethodName}` (e.g. `PersonService/GetPersonById`).
- This is the gRPC sibling of FastEndpoints' `app.UseFastEndpoints()`. Same app, same DI container, same process — **two protocols**, different audiences:
  - **FastEndpoints** → public REST/JSON for browsers & third parties.
  - **gRPC** → service-to-service calls between your own microservices (compact binary, HTTP/2, cancellable, streaming-capable).
- `EnableDetailedErrors`/`AddCodeFirstGrpc` don't replace `UseRouting` etc.; the gRPC endpoint goes through the same middleware pipeline.

**4. The implementation** (`PersonGrpcService.cs`)

```csharp
public class PersonGrpcService(PersonRepository _personRepository, GrpcTypeAdapterConfig _typeAdapterConfig)
    : IPersonService
```

- It's an ordinary class: primary constructor, DI-injected repository, `: IPersonService`. Nothing gRPC-specific about the class itself — that's the point of code-first.
- Return type is `ValueTask<T>`, not `Task<T>`: a struct-backed task that avoids an allocation when the call completes synchronously (common for cache hits/short queries). gRPC's code-first binder asks for `ValueTask`.
- Bodies are ordinary async code. `Get*` calls funnel through a shared helper:

```csharp
private async ValueTask<GetPersonResponse> GetPersonResponseAsync(Func<Task<Person?>> fetch)
{
    var person = Guard.NotFound(await fetch());
    return new GetPersonResponse { PersonInfo = person.Adapt<PersonInfo>(_typeAdapterConfig) };
}
```

- **The mapping is the reason `GrpcTypeAdapterConfig` exists.** The domain has rich value objects (`HonorificTitle`) and a nested `ProfessionalProfile` VO; the wire DTO has a plain `Honorific` enum and a flat proto message. Mapster's `Adapt<PersonInfo>(config)` needs explicit rules:

```csharp
this.NewConfig<Person, PersonInfo>()
    .Map(dest => dest.Honorific, src => src.Honorific == null ? null : src.Honorific.Value)
    .IgnoreNullValues(true);
this.NewConfig<Auth.Domain.Persons.ValueObjects.ProfessionalProfile, ProfessionalProfile>();
```

- Note the config is a **`TypeAdapterConfig` instance**, registered as a **singleton** (`services.AddSingleton<GrpcTypeAdapterConfig>()`) and passed explicitly into `Adapt`. The alternative is Mapster's static `TypeAdapterConfig.GlobalSettings`, but passing an instance keeps mapping rules per-boundary and testable.

### The client side — where code-first pays off (finished project only)

The current project only has the *server*. The finished project adds:

```csharp
// finished: Blocks.AspNetCore/Grpc/GrpcClientRegistrationExtensions.cs
var channel = GrpcChannel.ForAddress(serviceSettings.Url, new GrpcChannelOptions { ... });
return channel.CreateGrpcService<TClient>();     // TClient == IPersonService
```

- `GrpcChannel` is the long-lived connection to the remote host (it owns the HTTP/2 connection and should be reused, not recreated per call).
- `CreateGrpcService<IPersonService>()` returns a **dynamic proxy** implementing the interface — you get an object that *looks* like `IPersonService` but every call serializes over the wire. So the consuming service writes:

```csharp
var response = await _personService.GetPersonByIdAsync(new GetPersonRequest { PersonId = id });
```

…with no HttpClient, no route string, no JSON. That's the payoff of sharing the interface.
- The finished project registers it **scoped** (channel created per scope) with the alternatives deliberately left in the file as teaching comparisons: per-channel `RetryPolicy` at the gRPC layer, and a singleton channel.

### gRPC errors are *not* HTTP status codes

This is the single most important thing to internalize before the next section. When a gRPC call fails, the outcome is a **`Status`**: a `StatusCode` enum (`OK`, `NotFound`, `InvalidArgument`, `PermissionDenied`, `Unauthenticated`, `Unavailable`, `Internal`, `Unknown`, …) plus an optional message and trailing metadata. There is no "404" on the wire in the application sense — the gRPC server owns the HTTP response and always answers HTTP 200 with the real result in the **`grpc-status` trailer**.

Client-side, failures surface as **`RpcException`** (from `Grpc.Core`):

```csharp
try { ... }
catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound) { ... }
```

Also note the *transport* failures map to their own codes — server down = `Unavailable`, deadline exceeded = `DeadlineExceeded`, cancelled token = `Cancelled`. So retry/circuit-breaker logic keys off `Unavailable`, not off HTTP 5xx like in REST.

## gRPC exception handling ("gRPC exception middleware") — what it is

**Status in this project:** *not implemented.* The author mentions it; the finished project doesn't have a gRPC interceptor either — it has an **HTTP** `GlobalExceptionMiddleware` at `finished/src/BuildingBlocks/Blocks.AspNetCore/Middlewares/GlobalExceptionMiddleware.cs`. This note explains what the gRPC counterpart would be, because the distinction matters.

### Why the HTTP middleware doesn't solve this

`GlobalExceptionMiddleware` is a classic ASP.NET Core `RequestDelegate` wrapper: `try { await _next(context) } catch (...) { context.Response.StatusCode = ...; write JSON }`. That is the right mechanism for **REST** — it maps a domain exception to an HTTP status + a JSON body:

```csharp
private static HttpStatusCode MapStatusCode(Exception ex) => ex switch
{
    ValidationException  => HttpStatusCode.BadRequest,
    NotFoundException    => HttpStatusCode.NotFound,
    UnauthorizedException=> HttpStatusCode.Unauthorized,
    _                    => HttpStatusCode.InternalServerError
};
```

For gRPC that shape is wrong, for two reasons:

1. **The gRPC server swallows the exception first.** The ASP.NET Core gRPC call handler catches anything escaping your service method and converts it to a response with `StatusCode.Unknown` — the exception never propagates up the middleware pipeline, so `GlobalExceptionMiddleware` never gets to map it. Result: a `NotFoundException` thrown in `PersonGrpcService` reaches the client as a generic **`Unknown`**, indistinguishable from a genuine bug/crash.
2. **A gRPC client can't read a JSON body.** It expects the result in `grpc-status` trailers. An HTTP middleware writing `404` + a JSON body produces something the client reports as a framing/protocol failure, not a clean "not found".

So the mapping has to happen **inside the gRPC call**, where the server can still build a proper `Status` and trailers.

### What it is: a server-side `Interceptor`

An **interceptor** is gRPC's middleware — the analogue of an ASP.NET Core middleware, but at the RPC layer instead of the HTTP layer. It sits between the transport and your service method. The base type is `Grpc.Core.Interceptors.Interceptor`, which exposes one `virtual` method per call shape:

| Method | Call shape |
|---|---|
| `UnaryServerHandler<TRequest, TResponse>` | one request → one response (all methods in `IPersonService`) |
| `ServerStreamingServerHandler` | one request → stream of responses |
| `ClientStreamingServerHandler` | stream of requests → one response |
| `DuplexStreamingServerHandler` | stream ↔ stream |

Each receives a `continuation` delegate — "the rest of the pipeline", exactly like `_next(context)` in HTTP middleware. You call it, catch what comes out, and optionally translate.

### Two levels of implementation (verified against `protobuf-net.Grpc` 1.3.14)

**(a) Plain `Grpc.Core.Interceptors.Interceptor`** — works, but you write the `try/catch` boilerplate yourself in *every* streaming variant (`UnaryServerHandler` must be overridden, and if you ever add streaming methods, so must the others):

```csharp
public class GrpcExceptionInterceptor : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try { return await continuation(request, context); }
        catch (NotFoundException ex)  { throw new RpcException(new Status(StatusCode.NotFound, ex.Message)); }
        catch (ValidationException ex){ throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message)); }
        catch (Exception)             { throw new RpcException(new Status(StatusCode.Internal, "Internal error")); }
    }
}
```

**(b) The idiomatic code-first way — `ServerExceptionsInterceptorBase`.** protobuf-net.Grpc ships its own abstract base that already implements *all four* handler methods and funnels every exception into one overridable hook:

```csharp
// ProtoBuf.Grpc.Configuration.ServerExceptionsInterceptorBase : Grpc.Core.Interceptors.Interceptor
protected virtual bool OnException(Exception exception, out Status status);
```

- `abstract` base, no abstract members — you subclass and override only `OnException`.
- Return **`true`** → re-expose the exception to the client as an `RpcException` carrying the `out Status` you set.
- Return **`false`** → don't; the call stays a generic `Unknown` (the base default).
- Because it handles all four shapes for you, you never touch `UnaryServerHandler` and it works unchanged if a service later adds streaming.

A full example for this project (this is the "gRPC exception middleware" the author means):

```csharp
using Blocks.Exceptions;             // NotFoundException, BadRequestException, UnauthorizedException
using Grpc.Core;
using ProtoBuf.Grpc.Configuration;

namespace Auth.API.Infrastructure;

public class GrpcExceptionInterceptor : ServerExceptionsInterceptorBase
{
    protected override bool OnException(Exception exception, out Status status)
    {
        status = exception switch
        {
            NotFoundException      => new Status(StatusCode.NotFound,      exception.Message),
            ValidationException    => new Status(StatusCode.InvalidArgument, exception.Message),
            BadRequestException    => new Status(StatusCode.InvalidArgument, exception.Message),
            UnauthorizedException  => new Status(StatusCode.PermissionDenied, exception.Message),
            _                      => new Status(StatusCode.Internal, "Internal server error")
        };

        // Only the four known cases carry a safe message to the client; anything else
        // returns false so the client sees plain Unknown and the detail stays in your logs.
        return exception is NotFoundException or ValidationException
            or BadRequestException or UnauthorizedException;
    }
}
```

Two notes from the library's own XML docs, worth knowing:

- **`SimpleRpcExceptionsInterceptor`** (`ProtoBuf.Grpc.Configuration`) is a built-in implementation exposed as a singleton via `SimpleRpcExceptionsInterceptor.Instance`. Its `OnException` just re-exposes everything. Useful as a reference; not what you want in production since it leaks every exception message.
- **`[SimpleRpcExceptions]`** is an attribute (`AttributeUsage: Class | Method | Interface`) that opts a service/method/interface into *simplified* exception handling: all server exceptions get exposed to the caller with message and type. The docs explicitly warn this *"may present security implications"*, that extra metadata (`StatusCode`, etc.) is **not** propagated, and that the exception still arrives client-side as an `RpcException`. So it's the low-effort, less-safe option — putting it on `IPersonService` or `PersonGrpcService` is the one-liner version of the above.

**Registration** — via the same options object you already configure (`AddCodeFirstGrpc` returns `IGrpcServerBuilder`, and the delegate receives `Grpc.AspNetCore.Server.GrpcServiceOptions`, whose `Interceptors` is a collection):

```csharp
services.AddCodeFirstGrpc(options =>
{
    options.ResponseCompressionLevel = CompressionLevel.Fastest;
    options.EnableDetailedErrors = false;              // ← flips to false; the interceptor now maps errors
    options.Interceptors.Add<GrpcExceptionInterceptor>();
    // or, for the built-in leak-everything version:
    // options.Interceptors.Add(SimpleRpcExceptionsInterceptor.Instance);
});
```

Correction to what an earlier draft of this note said: you do **not** need `services.AddSingleton<GrpcExceptionInterceptor>()`. `InterceptorCollection.Add<T>()` constructs the interceptor via `ActivatorUtilities`, resolving its constructor dependencies from DI on the fly — register it separately only if you also inject it elsewhere. (If your interceptor takes a dependency, such as `ILogger<T>`, just put it in the primary constructor and let `ActivatorUtilities` supply it.)

Once this is in place, `EnableDetailedErrors` becomes unnecessary — it was standing in for exactly this mapping, and it's the reason your current `DependencyInjection.cs:43` leaks exception messages to callers.

### The mapping table to remember

| Domain exception | gRPC `StatusCode` | REST equivalent |
|---|---|---|
| `NotFoundException` | `NotFound` | 404 |
| `ValidationException` / `BadRequestException` | `InvalidArgument` | 400 |
| `UnauthorizedException` | `Unauthenticated` (bad/missing creds) or `PermissionDenied` (authenticated but forbidden) | 401 / 403 |
| anything unexpected | `Internal` | 500 |
| *not thrown by you*: server unreachable | `Unavailable` (transport) | connection refused |

Note `Unauthenticated` vs `PermissionDenied` — the split is finer than HTTP's 401/403 and is a common source of confusion the first time you see it.

### The alternative, and why not to do it

You *can* just `throw new RpcException(new Status(StatusCode.NotFound, "…"))` directly inside `PersonGrpcService`. It works, but it couples your service to `Grpc.Core` and hides the error contract inside method bodies. The interceptor keeps every mapping rule in one place, keeps the service class protocol-agnostic (the same `Guard.NotFound` still means "404" on the REST side), and is testable in isolation. Same reasoning as the HTTP case: don't scatter status codes through business code.

### Related gotchas worth knowing

- **This is why `Guard.NotFound` alone isn't enough for a gRPC endpoint.** It throws `NotFoundException` (which derives from `HttpException` and carries an HTTP status code) — a type the gRPC layer doesn't understand. Without the interceptor, the client sees `Unknown`. The interceptor is what translates the two worlds.
- The interceptor receives a raw **`ServerCallContext`**; your service method receives the code-first **`CallContext`** wrapper. Different types, same underlying call.
- Client-side, you can add the mirror-image retry/error interceptor (`Interceptor` on `channel.Intercept(...)`), but the finished project's retry is done at the transport layer instead (`RetryPolicy` in `GrpcClientRegistrationExtensions.cs`), deliberately — a Polly handler at the `HttpClient` layer never sees the `RpcException`, which is a trap worth remembering.

## DI registration: open generics, `typeof(...)`, and assembly scanning by base class

**File references:**
- `src/Articles/Services/Journals/Journals.Persistence/DependencyInjection.cs:24` — `services.AddScoped(typeof(Repository<>));`
- `src/Articles/BuildingBlocks/Blocks.Redis/Repository.cs` — the open generic being registered
- Consumers: `src/Articles/Services/Journals/Journals.API/Features/Journals/Create/CreateJournalEndpoint.cs:20-21` — injects `Repository<Journal>` and `Repository<Editor>`
- The EF Core analogue: `src/Articles/Services/Auth/Auth.Persistence/DependencyInjection.cs:17` — `services.AddScoped<PersonRepository>();` (one line per concrete repository)

The line that prompted this note:

```csharp
// register all repositories which have as a base class the generic Repository<T> class
services.AddScoped(typeof(Repository<>));
```

### First: the comment is misleading

The comment says *"register all repositories which have as a base class the generic `Repository<T>`"*. That is **not what this line does**, and there is no reflection or assembly scanning anywhere in the project (I checked — no `GetTypes()`, no Scrutor). Correct that sentence in your head as:

> *"register the open generic `Repository<T>` itself, so the container can manufacture `Repository<Journal>`, `Repository<Editor>`, … on demand."*

There's no list of repositories. Nothing is enumerated. Nothing is scanned. The finished project has the identical line and identical comment.

### What `typeof(Repository<>)` means

`Repository<>` is an **unbound generic type** — a type with a type parameter but no argument supplied. `typeof(Repository<>)` gives you its `Type` object. `Repository<Journal>` is a *constructed* (closed) generic, a completely different type to the CLR. `typeof(Repository<>)` has exactly one type parameter, so it's unbound; `typeof(Repository<,>)` would be the two-parameter version.

Because it's unbound, the codebase never writes `Repository<Journal>` as a `Type` in the registration — which is exactly why this pattern is attractive: you don't have to register each closed type.

### Why `typeof(...)` is needed at all — the two overloads

```csharp
// (1) generic method — the type argument is written as a type, resolved at COMPILE time
services.AddScoped<PersonRepository>();

// (2) non-generic overload, Type parameter — the type is a RUNTIME value
services.AddScoped(typeof(Repository<>));
```

You *cannot* write `services.AddScoped<Repository<>>();` — there is no syntax for an open generic as a generic method's type argument (CS7003: "Unexpected use of an unbound generic name"). The type-argument slot needs a complete type. So when the type you're registering can't be named completely, you drop to the non-generic overload and hand it a `Type` object. `typeof(...)` is how you obtain one for something that isn't a closed type.

Both overloads end up at the same internal `ServiceDescriptor`. The generic one is just sugar.

### What the container does with it

An **open generic registration** is a *factory rule*, not a concrete entry. At resolution time:

```
needs Repository<Journal>  →  finds open registration Repository<>  →  closes it at runtime: Repository<Journal>  →  constructor injection
```

So `Repository<Journal>` and `Repository<Editor>` both resolve from that single line. This is the resolution the two endpoint properties trigger:

```csharp
public class CreateJournalEndpoint(
    Repository<Journal> _journalRepository,
    Repository<Editor> _editorRepository, ...)
```

And `Repository<T>`'s constructor dependencies resolve normally inside the closed type:

```csharp
public Repository(IConnectionMultiplexer redis, RedisConnectionProvider provider)
```

That's why `AddSingleton<IConnectionMultiplexer>` and `AddSingleton(new RedisConnectionProvider(...))` appear just above — the open generic couldn't resolve without them.

Lifetime: `AddScoped` applied to the open registration applies to **every** closed type made from it. So `Repository<Journal>` is scoped, always.

### The real payoff, and what it costs

| | Registration lines | Add a new entity? |
|---|---|---|
| Explicit (EF Core side: `AddScoped<PersonRepository>()`) | one per concrete type | **edit DI** |
| Open generic (`AddScoped(typeof(Repository<>))`) | **one, ever** | **nothing** — `Repository<NewEntity>` just works |

The cost: an open generic only works when the implementation needs **no type-specific behaviour**. `Repository<T>` is a plain, concrete (`sealed`-ish) class with no overrides — the *same code* is correct for every `T`. The moment a repository needs its own methods, you write a concrete subclass and you're back to registering it explicitly. Compare the EF side, where `PersonRepository` exists precisely because it adds `GetByEmailAsync`, `GetByUserIdAsync`, and the `Include(p => p.User)` override — those can't live in the generic.

### The thing you actually asked about: registering *any* type with a given base class

The comment describes something real, but it needs **assembly scanning** — a line like the one in the comment does not do it. Which brings up the honest situation:

- **Nothing in this project scans assemblies by base class.** I grepped the whole solution: the only patterns are the two open generics (`Journals.Persistence:24`, `Submission.Persistence:20-21`), explicitly-listed concrete types (the EF side's `AddScoped<PersonRepository>()`), and library-provided assembly scans that key off *interfaces*, not base classes — MediatR's `RegisterServicesFromAssembly(Assembly.GetExecutingAssembly())` (already present in `Submission.Application/DependencyInjection.cs:22`) and FastEndpoints's `AddFastEndpoints()`. So scanning exists in the codebase, just never as "find everything deriving from `Repository<T>`".
- **Calling this "conventional registration" is a stretch** in the strict sense — that term means scanning the assembly and registering everything matching a rule.
- **"Open generic registration" is the precise name** for what this line is.

For completeness, the pattern the comment *implies* looks like this (using Scrutor, which most .NET codebases reach for; there's no built-in `IServiceCollection` API for it):

```csharp
// NOT in this project — illustration of "register everything deriving from Repository<T>"
services.Scan(scan => scan
    .FromAssemblyOf<JournalDbContext>()
    .AddClasses(c => c.AssignableTo(typeof(Repository<>)))    // open generic match
    .AsImplementedInterfaces()
    .WithScopedLifetime());
```

Two footguns if you ever do this:
- **`AssignableTo(typeof(Repository<>))`** works because `Repository<Journal>` *is* assignable to the open `Repository<>`. Registering it **`AsSelf()`** (not `AsImplementedInterfaces()`) matters here, since `Repository<T>` has no interface to expose.
- **Scanning runs at startup and silently registers whatever matches** — including abstract bases or types you didn't intend. It trades an explicit list for a convention you must document, or the next person can't tell what's registered. That's the real argument for the explicit line.

### Why `typeof(Repository<>)` is the sweet spot here

It gets the "one line covers every entity" benefit **without** any scanning: the container's open-generic support is built in, and `Repository<T>` needs no per-type behaviour. You get convention-like convenience with zero reflection and zero surprises. In this codebase that's the exception, not the rule — elsewhere (Auth, and the finished project generally) registration is explicit and per-type.

## CQRS with MediatR: commands, queries, handlers, pipeline behaviors

**File references (current project):**
- Command base: `src/Articles/Services/Submission/Submission.Application/Features/_Shared/ArticleCommand.cs`
- Command + handler + validator, side by side: `.../Features/CreateArticle/CreateArticleCommand.cs`, `CreateArticleCommandHandler.cs`, `CreateArticleCommandValidator.cs`
- Endpoint: `src/Articles/Services/Submission/Submission.Api/Endpoints/CreateArticleEndpoint.cs`
- Pipeline behaviors: `src/Articles/BuildingBlocks/Blocks.MediatR/Behaviors/ValidationBehavior.cs`, `SetUserIdBehavior.cs`
- Registration: `src/Articles/Services/Submission/Submission.Application/DependencyInjection.cs:17-27`
- Response DTO: `src/Articles/BuildingBlocks/Articles.Abstractions/IdResponse.cs`
- Marker interfaces (finished project only): `finished/src/BuildingBlocks/Blocks.MediatR/Abstractions/ICommand.cs`, `IQuery.cs`

### What "CQRS" means *here* — important calibration

CQRS (Command Query Responsibility Segregation) is often described as "separate databases for reads and writes", which is the heavyweight version (Event Sourcing-adjacent, read models projected from events). **That is not what this project does.** Here CQRS means something considerably smaller:

| Full CQRS | This project |
|---|---|
| Separate write store and read store | **One** `DbContext` / Redis per service |
| Read model is a projection built from events | Reads query the same tables directly |
| Eventually consistent | Strongly consistent (same transaction/context) |
| Commands and queries are different subsystems | Both are just MediatR request objects |

What's actually segregated is the **request shape and intent**, not the storage: a **command** is a request that *changes state* (`CreateArticleCommand`), a **query** is a request that *reads state* (`GetArticleQuery`). They're separate types, in separate folders, with separate handlers. That separation is genuinely useful on its own — it's the "CQRS-lite" the course means, and it's the right fit for a service-per-bounded-context microservices architecture where each service already owns its data.

### MediatR's job: an in-process mediator (not a message bus)

MediatR is a **library, not infrastructure** — no network, no broker, no serialization. It's an in-memory dispatcher that solves one problem: *"the thing that wants work done shouldn't know which class does it."*

```csharp
// The endpoint does this — it names no handler, no service, no implementation:
var response = await sender.Send(command);
```

At runtime MediatR looks up the single registered `IRequestHandler<CreateArticleCommand, IdResponse>` and calls its `Handle`. The endpoint depends only on `ISender` + the command type. Swap the handler, split it, add behaviours around it — the endpoint never changes.

Careful: MediatR is often confused with **MassTransit** (the message bus used for integration events between services, `IPublishEndpoint`). Different jobs:

| | MediatR | MassTransit |
|---|---|---|
| Scope | **within one process** | **across processes/services** |
| Transport | none (direct method call) | RabbitMQ / Azure Service Bus |
| Message type | in-memory object | serialized integration event |
| Failure mode | exception propagates | retries, dead-letter, eventual delivery |

Rule of thumb from the architecture guardrails: **domain events = within a service (MediatR/direct), integration events = across services (MassTransit).**

### The request → handler contract

MediatR has two generic roles:

```csharp
public interface IRequest<out TResponse> { }                 // "I am a request, answer with TResponse"
public interface IRequestHandler<in TRequest, TResponse>      // "I handle requests of this type"
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken ct);
}
```

A concrete pair from the project:

```csharp
// CreateArticleCommand.cs
public record CreateArticleCommand(int JournalId, string Title, ArticleType Type, string Scope)
    : ArticleCommand;

// CreateArticleCommandHandler.cs
internal class CreateArticleCommandHandler(Repository<Journal> _journalRepository)
    : IRequestHandler<CreateArticleCommand, IdResponse>
{
    public async Task<IdResponse> Handle(CreateArticleCommand command, CancellationToken ct)
    {
        var journal = await _journalRepository.FindByIdOrThrowAsync(command.JournalId);
        var article = journal.CreateArticle(command.Title, command.Type, command.Scope);
        await AssignCurrentUserAsAuthor(article, command);
        await _journalRepository.SaveChangesAsync(ct);
        return new IdResponse(article.Id);
    }
}
```

Things to notice:

- **The command is a `record` with `init` properties** — an immutable data bag. Commands carry *data*, never behaviour or dependencies. (MediatR can bind it straight from the JSON body, which is why it's also the API request contract.)
- **`Handle` is generic-free at the call site** — the type arguments live on the class, so `sender.Send(command)` infers everything.
- **The handler is `internal`** — nothing outside the Application assembly has any business calling it; communication goes through `ISender`. A nice encapsulation detail.
- **The response is `IdResponse`** (`record IdResponse(int Id)` in `Articles.Abstractions`) — a tiny shared DTO so every "create" command can answer with an id without each feature inventing its own response type.
- **`TResponse` can be `Unit`** (MediatR's void) for commands that return nothing. The finished project models this with `ICommand : ICommand<Unit>`.
- **Handlers are thin** — they orchestrate: load aggregate → call **domain methods** (`journal.CreateArticle(...)`) → save. Note `CreateArticle` is a *method on the `Journal` aggregate*, not logic in the handler. That's the architecture guardrail ("no service layer classes") showing up in practice: the handler is a use-case coordinator, the rules live in the domain.

### The query side

Queries look the same shape but must not mutate:

```csharp
// finished: GetArticleQuery.cs
public record GetArticleQuery(int ArticleId) : IQuery<GetArticleResponse>;
public record GetArticleResponse(ArticleDto ArticleSummary);

// finished: GetArticleQueryHandler.cs
public class GetArticleQueryHandler(ArticleRepository _articleRepository)
    : IRequestHandler<GetArticleQuery, GetArticleResponse>
{
    public async Task<GetArticleResponse> Handle(GetArticleQuery command, CancellationToken ct)
    {
        var article = Guard.NotFound(await _articleRepository.GetFullArticleByIdAsync(command.ArticleId));
        return new GetArticleResponse(article.Adapt<ArticleDto>());
    }
}
```

- **Current-project status:** there are **no query handlers yet** — only the four command handlers (`CreateArticle`, `AssignAuthor`, `CreateAndAssignAuthor`, `UploadManuscriptFile`). Queries arrive in the finished project (`GetArticle`, `DownloadFile`, `GetArticleInvitations`).
- The query side is where you'd reach for read optimizations that commands shouldn't use: `AsNoTracking()`, projections straight to a DTO (`Select(...)`) instead of loading aggregates, or a dedicated read store. In the finished project, `GetFullArticleByIdAsync` is a purpose-built read method on the repository for exactly this.
- **Naming nit:** the query handler's parameter is named `command`. Harmless (MediatR doesn't care) but worth renaming to `query` for readability — the whole point of separating the two sides is that a reader can tell at a glance which they're looking at.

### Project marker interfaces: `IRequest` vs `ICommand`/`IQuery`

Your current code inherits MediatR's `IRequest<TResponse>` directly. The **finished project adds thin marker interfaces** on top:

```csharp
// finished: Blocks.MediatR/Abstractions/ICommand.cs
public interface ICommand : ICommand<Unit> { }
public interface ICommand<out TResponse> : IRequest<TResponse> { }

// finished: Blocks.MediatR/Abstractions/IQuery.cs
public interface IQuery<out TResponse> : IRequest<TResponse> where TResponse : notnull { }
```

They add **no members** — they're purely for intent and for targeting:

- A pipeline behavior or convention can say `where TRequest : IQuery<TResponse>` and act on *all queries* without touching commands (e.g. attach `AsNoTracking`, add read caching).
- A reader sees `IQuery<GetArticleResponse>` and knows instantly this must not mutate.
- Note they still derive from `IRequest<TResponse>`, so MediatR's machinery is untouched — this is a naming layer, not a different mechanism.

Because they're empty, they cost nothing at runtime. Add them when you start writing query handlers; before that they'd be interfaces with a single implementation, which the guardrails advise against.

### The vertical slice layout

CQRS meets feature organisation here: each use case is a folder containing everything it needs, rather than a technical layer shared across all features.

```
Submission.Application/Features/
├── _Shared/ArticleCommand.cs          ← shared command base
├── CreateArticle/
│   ├── CreateArticleCommand.cs        ← request + (finished) validator, same file
│   ├── CreateArticleCommandHandler.cs
│   └── CreateArticleCommandValidator.cs
├── AssignAuthor/…
└── UploadFile/UploadManuscriptFile/…
```

Contrast with a horizontal layout (`Commands/`, `Handlers/`, `Validators/` folders). The slice wins because **one feature = one folder**: you open `CreateArticle/` and see the whole use case. It's also what lets the Clean Architecture layering (`.API` / `.Application` / `.Domain` / `.Persistence`) and the slicing coexist — the layering is structural, the slicing is organisational inside `.Application`.

Adding a feature = add a folder. No central registration, no list to update — see the scanning section below.

### Pipeline behaviors — the decorator chain around every handler

This is the most conceptually important part. A `IPipelineBehavior<TRequest, TResponse>` wraps the handler, exactly like ASP.NET Core middleware wraps a request:

```csharp
public interface IPipelineBehavior<TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct);
}
```

`next` is the rest of the chain (and, at the end, your handler) — identical in spirit to `_next(context)` in `GlobalExceptionMiddleware` and `continuation` in a gRPC interceptor. Three flavours of the same idea, in three layers of the stack.

**`ValidationBehavior`** — plugs FluentValidation in front of every handler:

```csharp
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.Where(r => r.Errors.Any()).SelectMany(r => r.Errors).ToList();
        if (failures.Any())
            throw new ValidationException(failures);
        return await next();      // ← handler only runs if validation passed
    }
}
```

The clever bit is `IEnumerable<IValidator<TRequest>>`: for a given request type the container supplies *every* registered validator for it (usually zero or one). No validators → empty enumerable → loop does nothing → passthrough. That's why the behavior can be global and generic with no per-request wiring: **validators are matched by generic type, automatically.**

Throwing `ValidationException` (FluentValidation's type) is what `GlobalExceptionMiddleware` maps to **400** — that's the whole contract in one line:

```csharp
ValidationException => HttpStatusCode.BadRequest,
```

(For gRPC, this is the case the interceptor would map to `InvalidArgument` — see the gRPC section above.)

**`SetUserIdBehavior`** — populates the audit fields before the handler runs:

```csharp
public class SetUserIdBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuditableAction
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        request.CreatedById = 1;   // ← stub; finished project reads the JWT claims instead
        return await next();
    }
}
```

- Note the constraint `where TRequest : IAuditableAction` — this behavior only runs for requests implementing that interface. **That's the targeting mechanism**: behaviors filter themselves by constraint, so unrelated requests pass through untouched.
- `IAuditableAction` (`Blocks.Domain/IAuditableAction.cs`) is where `CreatedById` and `CreatedOn` live, and `ArticleCommand` gets them via `IArticleAction : IAuditableAction`. Every command that tracks who did it gets stamped for free.
- **Current state:** `CreatedById = 1` is a hardcoded stub. The finished project has `AssignUserIdBehavior` reading `IClaimsProvider` instead, plus an `AssignUserIdPreProcessor` on the FastEndpoints side (and an `AssignUserIdFilter`). Worth flagging as a known placeholder — it means every audit record currently says user 1.

**Order matters.** Behaviors run in registration order, outermost first. So:

```
ValidationBehavior → SetUserIdBehavior → YourHandler → (unwind back out)
```

Validation runs before the user id is stamped. That ordering is deliberate here, but note it's only guaranteed by registration order — if a behavior must run first, register it first.

### Registration — why you never list handlers

```csharp
// Submission.Application/DependencyInjection.cs
services
    .AddValidatorsFromAssemblyContaining<CreateArticleCommandValidator>()   // FluentValidation: find all validators
    .AddMediatR(config =>
    {
        config.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());  // find all handlers
        config.AddOpenBehavior(typeof(ValidationBehavior<,>));                 // add behaviors to the chain
        config.AddOpenBehavior(typeof(SetUserIdBehavior<,>));
    });
```

- **`RegisterServicesFromAssembly`** scans the assembly for every `IRequestHandler<,>` / `INotificationHandler<>` and registers it. This is **interface-based scanning** — the same mechanism as MediatR elsewhere in the codebase and FastEndpoints's `AddFastEndpoints()`. It's the opposite of the explicit `AddScoped<PersonRepository>()` style and the reason a new feature folder needs zero DI changes.
- **`AddOpenBehavior(typeof(ValidationBehavior<,>))`** is the open-generic registration from the previous note, applied to behaviors: register `ValidationBehavior<,>` once, and it's constructed for *every* `(TRequest, TResponse)` pair the container resolves. `AddOpenBehavior` also takes a `ServiceLifetime`.
- `AddValidatorsFromAssemblyContaining<T>` is FluentValidation's own scanning helper — it registers every `AbstractValidator<T>` as `IValidator<T>`, which is exactly what `IEnumerable<IValidator<TRequest>>` in `ValidationBehavior` consumes. The two libraries are designed to snap together like this.
- Note the validator can live in the command file (finished project) or its own file (current project) — same assembly, so scanning finds it either way.

### The endpoint: thin by design

```csharp
app.MapPost("api/articles", async (CreateArticleCommand command, ISender sender) =>
{
    var response = await sender.Send(command);
    return Results.Created($"/api/articles/{response.Id}", response);
})
.RequireAuthorization(policy => policy.RequireRole(Role.AUT));
```

- **`ISender` vs `IMediator`:** `ISender` exposes only `Send`; `IMediator` adds `Publish` (notifications) and `CreateStream`. Requesting the narrower interface documents that this endpoint only sends requests — a small but deliberate choice.
- The endpoint does routing, auth, HTTP status mapping. All rules, validation, audit, persistence and domain logic sit behind `Send`. It's the Minimal-API equivalent of the FastEndpoints style used in Auth.
- Because the command is bound from the body as-is, **the command doubles as the public API contract** — which is why commands must not grow handler-only fields. (The finished project handles this with `[JsonIgnore]` on `ArticleId` and `CreatedById`, exactly so clients can't set them.)

### MediatR basics worth having straight

| Concept | Meaning |
|---|---|
| `IRequest<TResponse>` | A request expecting `TResponse`. **Exactly one** handler must exist, or `Send` throws at runtime |
| `IRequestHandler<TReq, TRes>` | The single handler for `TReq` |
| `INotification` / `INotificationHandler<T>` | A *notification* (event): **zero or many** handlers, each called in turn. No response |
| `Unit` | MediatR's void — `IRequest<Unit>` for a command with no return value |
| `Send` vs `Publish` | `Send` = one handler, returns a value (commands & queries). `Publish` = fan-out to all handlers, returns nothing (in-process events) |
| `IPipelineBehavior<,>` | Cross-cutting wrapper; `next()` continues the chain |

The `IRequest` vs `INotification` distinction is the one to internalize: **a command/query is a request (one handler); a domain event is a notification (many handlers, no answer).** The ArticleTimeline module in the finished project is built on that second pattern — it reacts to domain events raised by aggregates, which is how a stage transition gets recorded without the aggregate knowing anything about the timeline.

### Where the domain event actually fires

Worth noting because it ties the layering together: the handler calls `SaveChangesAsync`, but the **domain events** raised by `journal.CreateArticle(...)` are dispatched by an EF Core `SaveChangesInterceptor` (`Blocks.EntityFrameworkCore/Interceptors/` in the finished project), not by the handler. So a single `SaveChangesAsync` can both persist the article and trigger every reaction to it — and the handler still doesn't know who's listening. MediatR's `Publish` is the in-process mechanism underneath that.

## Where extension methods live (and how to find them)

**File references:**
- The prompt for this note: `src/Articles/BuildingBlocks/Blocks.Core/Extensions/StringExtensions.cs`
- Sibling: `BuildingsBlocks/Blocks.Core/Extensions/DateTimeExtensions.cs`, `EnumerableExtensions.cs`

### The annoyance that makes this worth a note

An extension method is **not discoverable from the type it extends**. `"abc".ToCamelCase()` doesn't appear in `string`'s IntelliSense unless the compiler knows about the *static class* holding it — which means the calling file needs a `using` for that class's **namespace**. Miss the `using` and the method looks like it doesn't exist; the call site gives you no clue that a helper is one namespace away.

So the question "where do extension methods live?" is really two questions: *where's the file*, and *what namespace do I need to import to use it?*

### The answer here: a convention of `<Target>Extensions` classes

The codebase follows a consistent shape. Class named after the target type, method in a `static class`, `this` on the first parameter:

```csharp
// Blocks.Core/Extensions/StringExtensions.cs
namespace Blocks.Core.Extensions;

public static class StringExtensions
{
    public static string ToCamelCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
```

- `namespace Blocks.Core.Extensions` — the namespace is **per folder**, so `Extensions/` → `Blocks.Core.Extensions`. This is why the class name and the folder both encode the same convention.
- `static class` — required: extension methods can only live in a non-nested, non-generic static class.
- `this string value` — the first parameter is the receiver. Everything after it are normal arguments.
- Note `ToCamelCase` is a `string` extension, but it's callable on a **nullable** `string?` without a warning *inside* the method, because the first line handles null/empty. Callers passing `null` get `null` back rather than an NRE — deliberate, since a null-forgiving return of `null` is more useful than an exception for a formatting helper.

### The full map of extension classes in this project

Grouped by what they extend — this is the "where do I look" answer:

| Extends | Class | Namespace | File |
|---|---|---|---|
| `string` | `StringExtensions` | `Blocks.Core.Extensions` | `Blocks.Core/Extensions/StringExtensions.cs` |
| `DateTime` | `DateTimeExtensions` | `Blocks.Core` ⚠️ | `Blocks.Core/Extensions/DateTimeExtensions.cs` |
| `IEnumerable<T>` | `EnumerableExtensions` | `Blocks.Core.Extensions` | `Blocks.Core/Extensions/EnumerableExtensions.cs` |
| `IConfiguration` | `ConfigurationExtensions` | `Blocks.Core` ⚠️ | `Blocks.Core/ConfigurationExtensions.cs` |
| `IServiceCollection` | `ConfigurationExtensions` | `Blocks.Core` | `Blocks.Core/ConfigurationExtensions.cs` |
| `IMemoryCache` | `MemoryCacheExtensions` | `Blocks.Core.Cache` | `Blocks.Core/Cache/MemoryCacheExtensions.cs` |
| `IRuleBuilder<…>` | `Extensions` | `Blocks.Core.FluentValidation` | `Blocks.Core/FluentValidation/Extensions.cs` |
| `HttpContext` / `HttpRequest` | `Extensions` | `Blocks.AspNetCore` | `Blocks.AspNetCore/Extensions/Extensions.cs` |
| `Repository<,>` / `DbSet<T>` | `RepositoryExtensions` | `Blocks.EntityFrameworkCore` | `Blocks.EntityFramework/RepositoryExtensions.cs` |
| `PropertyBuilder`, `ComplexTypePropertyBuilder` | `BuilderExtensions` | `Blocks.EntityFrameworkCore` | `Blocks.EntityFramework/BuilderExtensions.cs` |
| `IRedisCollection<T>`, `IDatabase`, `RedisConnectionProvider` | `Extensions` | `Blocks.Redis` | `Blocks.Redis/Extensions.cs` |
| `TBuilder` (authorization) | `Extensions` | `Articles.Security` | `Articles.Security/Extensions.cs` |
| `EmailAddress`, `EmailMessage` | `Extensions` | `EmailService.Smtp` | `Modules/EmailService/EmailService.Smtp/Extensions.cs` |
| `string` (file paths) | `FileExtensions` | `Submission.Domain.ValueObjects` | `Submission.Domain/ValueObjects/FileExtensions.cs` |

⚠️ **The trap:** `DateTimeExtensions.cs` and `EnumerableExtensions.cs` sit **side by side in the same `Extensions/` folder** but declare **different namespaces** — `DateTimeExtensions` is in `Blocks.Core`, the other two in `Blocks.Core.Extensions`. So seeing the file location doesn't tell you the namespace you must import. Always check line 1 of the file. (`DateTimeExtensions.cs:1` — that inconsistency is real, not a transcription slip.)

### Why the class names are inconsistent too

Three naming styles coexist, and it's worth knowing they're all equally "correct":

| Style | Example | When it appears |
|---|---|---|
| `<Target>Extensions` | `StringExtensions`, `RepositoryExtensions`, `MemoryCacheExtensions` | one target type, or one obvious theme |
| `Extensions` (bare) | `Blocks.AspNetCore/Extensions/Extensions.cs`, `Blocks.Redis/Extensions.cs` | a grab-bag: several unrelated targets in one file |
| `BuilderExtensions` / `FileExtensions` | EF builders; file-path helpers | domain-flavoured grouping rather than a single type |

The bare `Extensions` class is a mild smell — it names the *mechanism* rather than the *content*, so `Extensions.GetClientIpAddress(...)` tells you little. It's the extension-method equivalent of a `Helpers/` folder. Not worth refactoring now, but if you add to one, prefer a name that says what it's for.

### The general rule for finding any extension method

Given a call that looks like it should be a member but isn't:

1. **Check what it extends.** `someString.ToCamelCase()` → it must be a `string` extension.
2. **Search the solution** rather than guessing the namespace:
   ```bash
   grep -rn --include='*.cs' 'ToCamelCase(this \|this string' src/Articles
   ```
   Grepping for `this <Type>` finds every extension of that type, wherever it lives.
3. **Read line 1 of the file** for the namespace, then add the `using`.
4. **If the call compiles for someone else but not you**, a missing `using` is the overwhelmingly likely cause — not a missing method.

### Gotchas specific to extension methods

- **They can't access private members** of the type they extend. `StringExtensions` is an outsider to `string`, using only public API. (Contrast `ReportGenerator`, which lives *inside* `Blocks.Reporting` and can see internals — same method shape, different visibility.)
- **Extension methods lose to instance methods.** If a type has a real method with the same signature, the instance method always wins; the extension is never called. This is why `MemoryCacheExtensions.Get<T>()` coexists with `IMemoryCache`'s own members without conflict — different names.
- **They're resolved at compile time** and are essentially sugar for a static call: `someString.ToCamelCase()` compiles to `StringExtensions.ToCamelCase(someString)`. No polymorphism, no virtual dispatch. If the receiver is `null`, the method *is still called* and receives `null` — hence the null-handling in helpers like `IsNullOrEmpty`/`ToCamelCase`.
- **Static-import warning:** the finished project has `RepositoryExtensions.cs` as a `static class`, and there's a copy of that file in your project as `RepositoryExtensions.cs`. Extension classes must be `static`; a non-static class containing `this` methods is a compile error.

### Current usage state (things that exist but nothing calls)

Verified by grepping for call sites — several `StringExtensions` members are currently **dead code**, presumably added ahead of the videos that use them:

| Member | Called from |
|---|---|
| `FormatWith` | `Blocks.Core/FluentValidation/Extensions.cs` ✅ |
| `ToUnixEpochDate` | `Auth.Application/TokenFactory.cs:47` ✅ |
| `ToCamelCase` | *nothing yet* |
| `EqualsIgnoreCase`, `EqualsOrdinalIgnoreCase` | *nothing yet* |
| `GetClientIpAddress`, `BaseUrl` | *nothing yet* |

Also worth knowing: `StringExtensions.IsNullOrEmpty` **duplicates** `Blocks.Core/Guard.IsNullOrEmpty` (which is itself an extension method — see the `Guard` note above, where mixing statics and extensions in one class was flagged). Two spellings of the same thing in two namespaces is exactly how a "which one do I use?" problem starts; the finished project moves these to a `GuardExtensions` class to clean it up.

## CancellationToken: how to use it

**File references (where it appears in this project):**
- FastEndpoints auto-bound param: `src/Articles/Services/Auth/Auth.API/Features/Users/CreateUser/CreateUserEndpoint.cs:23` — `HandleAsync(CreateUserCommand command, CancellationToken ct)`
- gRPC: `src/Articles/Services/Auth/Auth.API/Features/Persons/PersonGrpcService.cs:14,21` — `context.CancellationToken`
- MediatR: `.../CreateArticle/CreateArticleCommandHandler.cs:16` — `Handle(..., CancellationToken ct)`
- Repository convention: `src/Articles/Services/Auth/Auth.Persistence/Repositories/PersonRepository.cs:13` — `GetByEmailAsync(string email, CancellationToken ct = default)`
- Plumbed to EF Core: `src/Articles/BuildingBlocks/Blocks.EntityFramework/Repository.cs:62` — `SaveChangesAsync(CancellationToken ct)`
- Plumbed to SMTP: `src/Articles/Modules/EmailService/EmailService.Smtp/SmtpEmailService.cs:18,28-31`
- Request-aborted (finished): `finished/.../ArticleAccessAuthorizationHandler.cs:26` — `_httpContext.RequestAborted`

### The mental model: two objects, two roles

| Type | Role | Who uses it |
|---|---|---|
| `CancellationTokenSource` (`cts`) | **Owns** cancellation. Has `Cancel()`. | The code that *decides* to stop |
| `CancellationToken` (`ct`) | **Observes** cancellation. Read-only flag. | Everyone who wants to *know* it stopped |

`cts.Token` is the token you hand out. `cts.Cancel()` flips it; every holder sees `IsCancellationRequested == true`. There is **no forced abort** — cancellation is *cooperative*: nothing stops until your code checks the flag or hands the token to an API that does.

This is the single most important thing: **a token that is never passed anywhere does nothing.** It's not a kill switch; it's a message you have to deliver.

### Where the token comes from (you rarely create one yourself)

In a request-handling flow, the framework already made one for you:

| Context | How you get `ct` |
|---|---|
| FastEndpoints | auto-bound parameter: `HandleAsync(cmd, CancellationToken ct)` — nothing to wire |
| MediatR handler | `Handle(TRequest request, CancellationToken ct)` — supplied by MediatR |
| gRPC | `context.CancellationToken` off the `CallContext` |
| Raw ASP.NET Core | `HttpContext.RequestAborted` |
| Outbound `HttpClient` | pass `ct` to `SendAsync` |
| gRPC client | pass `ct` in `CallOptions` |

In all of the first four, the token is **already tied to the client connection** — if the browser disconnects mid-request, it fires. That's why it's threaded all the way down: so a disconnected client can abort the SQL query instead of letting it run to completion.

### Example 1 — receiving and passing it down (the 90% case)

This is the shape used everywhere in the project. From `CreateUserEndpoint.cs`:

```csharp
public override async Task HandleAsync(CreateUserCommand command, CancellationToken ct)
{
    var person = await _personRepository.GetByEmailAsync(command.Email, ct);   // → repository → EF

    await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

    ...
    await _personRepository.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
}
```

...and the repository passes it into EF Core, which passes it into the SQL command:

```csharp
// PersonRepository.cs
public async Task<Person?> GetByEmailAsync(string email, CancellationToken ct = default)
    => await Query().SingleOrDefaultAsync(p => p.Email.NormalizedEmail == email.ToUpperInvariant(), ct);
//                                                                                                 ^^
//                                           EF Core turns this into a DB command that can be cancelled
```

**The rule: every `await` on an operation that accepts a token should get the token.** If you drop it at any hop, cancellation stops working for everything below that hop.

Same pattern for gRPC, where the token comes off `CallContext` rather than a parameter:

```csharp
// PersonGrpcService.cs
public async ValueTask<CreatePersonResponse> GetOrCreatePersonAsync(
    CreatePersonRequest request, CallContext context = default)
{
    var person = await _personRepository.GetByEmailAsync(request.Email, context.CancellationToken);
    ...
    await _personRepository.SaveChangesAsync(context.CancellationToken);
}
```

### Example 2 — the `= default` convention on library methods

Repository and service methods declare `CancellationToken ct = default`:

```csharp
public async Task<Person?> GetByEmailAsync(string email, CancellationToken ct = default)
```

Why `= default`: **`default` is `CancellationToken.None`** — a token that can never be cancelled. This makes the parameter *optional*, so a caller that has no token (a test, a startup job, a seeding routine) can omit it, while a request handler passes a real one. It's a deliberate escape hatch, not laziness — but it does mean forgetting to pass `ct` fails **silently**: the call still works, it just can no longer be cancelled.

### Example 3 — creating your own (timeout / background work)

Inside a request you normally don't, but this is how when you need to:

```csharp
// A timeout: cancel this work if it takes longer than 5 seconds
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
// or, equivalently:
using var cts2 = new CancellationTokenSource();
cts2.CancelAfter(TimeSpan.FromSeconds(5));

await _fileService.UploadFileAsync(filePath, file, cts.Token);
```

`CancellationTokenSource(TimeSpan)` starts the clock immediately. Always `using` it — a CTS holds a timer and, once things register on it, must be disposed.

**Linking** two tokens — e.g. "cancel if the client disconnects **or** we hit our own 30s budget":

```csharp
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutCts.Token);

await _slowClient.CallAsync(request, linked.Token);   // cancelled by either
```

`CreateLinkedTokenSource` is the standard way to combine a caller's token with your own deadline — you don't pick one.

### Example 4 — checking the flag yourself (CPU-bound loops)

For work where there's no async API to hand the token to, poll:

```csharp
foreach (var row in rows)
{
    ct.ThrowIfCancellationRequested();   // throws OperationCanceledException
    Process(row);
}
```

`ThrowIfCancellationRequested()` is the idiomatic check — it throws `OperationCanceledException` for you, which is why it's preferred over `if (ct.IsCancellationRequested) return;` (the latter tells the caller "finished" when you actually bailed).

### What happens when it fires

Cancellation surfaces as **`OperationCanceledException`** (or `TaskCanceledException`, its subclass — that's what `HttpClient` throws). It propagates up like any exception and, in ASP.NET Core, becomes a **499** (client closed request) rather than a 500. `GlobalExceptionMiddleware` is aware of this:

```csharp
catch (OperationCanceledException) // response is likely abandoned by client
{
    if (!context.Response.HasStarted)
        context.Response.StatusCode = 499;
}
```

So you generally don't catch it yourself — let it bubble. Only catch it if you must clean up *and* rethrow:

```csharp
try
{
    await _fileService.TryDeleteFileAsync(fileId, ct);
}
catch (OperationCanceledException)
{
    // swallowed deliberately here — cleanup, not the main work
}
```

### Gotchas

- **Passing `ct` and then not checking it is useless.** The token only takes effect where it reaches a cancellable API or an explicit `ThrowIfCancellationRequested`.
- **It cannot stop synchronous CPU work.** `Thread.Sleep`, a long `for` loop with no check, a blocking `Stream.Read` — a token does nothing for those. They must poll.
- **It cannot roll back what already happened.** Cancellation during a transaction rolls back *if* the transaction is tied to it; otherwise earlier writes stand. That's why `CreateUserEndpoint` threads `ct` into `BeginTransactionAsync`/`CommitAsync`.
- **Don't reuse a `CancellationTokenSource`.** Once cancelled, it stays cancelled; create a fresh one per operation.
- **Don't dispose a CTS while work still uses its token** — you'll get `ObjectDisposedException` at a confusing place. `using` it at the correct scope matters.
- **`default`/`CancellationToken.None` never cancels** — fine for tests, dangerous in production paths where you thought you were cancellable.

### Things to fix in the current code (found while checking this)

- `CreateUserEndpoint.cs:36` — `await _userManager.CreateAsync(user);` **doesn't pass `ct`**, while every line around it does. `UserManager.CreateAsync` accepts a token, so this is an inconsistency, likely an oversight.
- `CreateUserEndpoint.cs:49` — `await PublishAsync(new UserCreated(...));` likewise has no token.
- `CreatePersonAsync` at `:61` calls `_personRepository.AddAsync(person)` — but `Repository<TContext,TEntity>.AddAsync` **has no `CancellationToken` parameter at all** (`Repository.cs:44`), so it *can't* be cancelled there. The finished project adds `ct` to these repository methods. Worth knowing as a limitation, not a bug you introduced.
- **Naming inconsistency:** handlers use both `ct` and `cancellationToken` (`AssignAuthorCommandHandler.cs:9` vs `CreateArticleCommandHandler.cs:16`). Harmless, but the codebase would be easier to scan with one spelling. The BCL's own convention in the interfaces is `cancellationToken`; the project's shorthand is `ct`.

## CancellationToken: can you inject it, or is it always prop drilling?

**The question:** in Vue you can `inject()` a value instead of drilling it through every component's props. Can you do the same with `CancellationToken`, or does it always have to be passed parameter by parameter?

**Short answer:** you *can* make it ambient, and .NET has a real equivalent of provide/inject — but you shouldn't use it inside handlers and repositories. Pass it explicitly. The reason is not stylistic; the ambient version silently stops working on exactly the paths that need cancellation most.

### Why DI can't just hand you a token

The tempting idea: `CancellationToken` is per-request, `HttpContext` is per-request, the scope *is* per-request — so inject it like any other scoped service.

It doesn't work because of **when** DI runs:

| | Vue `provide`/`inject` | .NET DI |
|---|---|---|
| Kind of lookup | runtime, walks live tree per render | construction-time, builds a static graph |
| Granularity | re-resolves continuously | resolves **once** per scope (or singleton/transient) |
| Can carry per-call data? | yes | **no** — a singleton can't have a per-call value |

A `CancellationToken` isn't a *service*, it's **per-call data** that differs on every single invocation, even within one scope. There's nothing for the container to bind it to — if you registered it, you'd have to pick one token at scope creation and freeze it for everything resolved in that scope, which is wrong for a scope that serves more than one operation. This is precisely why MediatR's `IRequestHandler<TRequest, TResponse>.Handle` takes it as a **parameter** and not a constructor dependency. The same is true of `IPipelineBehavior`, ASP.NET's action methods, and EF Core's `SaveChangesAsync`.

So the answer splits in two:

- **Within a handler/controller you cannot avoid the parameter** — the framework contract *is* a parameter.
- **Below that** (repositories, services) you *can* avoid it via an ambient provider — that's the Vue-style escape hatch.

### The actual "inject" analog: `IHttpContextAccessor` + `AsyncLocal`

Vue's inject is ergonomic because component renders form a live tree. `AsyncLocal<T>` is what .NET has in that role: a value written once that **flows down the async call chain** automatically. `IHttpContextAccessor` is a thin public wrapper over exactly that — this is the real provide/inject of .NET, and it's why you can read `HttpContext` deep in a library with no `HttpContext` parameter anywhere.

An ambient token provider built on it:

```csharp
public interface IRequestCancellation
{
    CancellationToken Token { get; }
}

internal sealed class HttpRequestCancellation(IHttpContextAccessor accessor) : IRequestCancellation
{
    // AsyncLocal-backed → resolves to whatever request is on THIS async flow
    public CancellationToken Token => accessor.HttpContext?.RequestAborted ?? CancellationToken.None;
}

// registration
services.AddHttpContextAccessor();
services.AddSingleton<IRequestCancellation, HttpRequestCancellation>();
```

Then the repository injects it and call sites pass nothing:

```csharp
public class ArticleRepository(SubmissionDbContext db, IRequestCancellation cancellation)
{
    public Task<Article> FindByIdAsync(int id)
        => Query().FirstAsync(a => a.Id == id, cancellation.Token);
}

// handler — no ct anywhere
var article = await _articleRepository.FindByIdAsync(command.ArticleId);
```

It genuinely works, and in a pure-HTTP monolith it looks very clean. Here's why it's still the wrong default.

### Why ambient token is a trap (the part that matters)

1. **Half your code has no `HttpContext`.** Your ArticleHub MassTransit consumers, your Person/Journal gRPC *servers*, `IHostedService` background workers, EF migrations, the outbox dispatcher — none of these have an HTTP request. `accessor.HttpContext` is `null`, so the provider returns `CancellationToken.None` and **cancellation silently disappears**. Vue has one tree; .NET has HTTP, gRPC, message consumers, and hosted services, each with a different token source. An ambient provider only knows about one of them. It fails *silently* — the worst possible failure mode, since the code still compiles and still works, just never cancels.
2. **Tokens aren't "the request token", they're values.** Real code narrows them: `CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutToken)` for a 30s budget, or a per-operation `cts`. Ambient lookup gives you one fixed token and hides the fact that the narrower one exists. You lose Example 3 from the previous section.
3. **You lose the analyzer.** `CA2016` ("forward the `CancellationToken` parameter to methods that take one") only fires when the token is *a parameter*. Compiler-enforced forwarding is what actually keeps a codebase honest over years — with ambient tokens, cancellation becomes unauditable: you can't tell from a signature whether a method is cancellable.
4. **Tests get harder.** A unit test can pass `new CancellationToken(true)` straight in; with ambient you must construct an `HttpContext`/`IHttpContextAccessor` fixture first.
5. **It hides the cost.** Explicit drilling makes the token's absence visible in a diff. Ambient tokens make a dropped token invisible — which is exactly how your `ApproveArticleCommandHandler` ended up dropping `ct` in three places without anyone noticing.

There's an interesting middle ground if you *do* want the convenience: **keep the ambient provider, but only as a fallback at the outermost layer, and still accept an explicit token everywhere.** The parameter wins when supplied, the ambient value is the default when it isn't. That's the only version that stays correct across transport types — but note it's now more code than just passing the token.

### The pattern to actually use: CQRS / MediatR

**Edge → handler → repository → EF, one `ct` parameter per hop.** Four levels, all explicit:

```csharp
// 1. EDGE (minimal API): add the parameter, ASP.NET binds RequestAborted to it.
//    This is the ONE place you get a token "for free".
app.MapPost("/articles/{articleId:int}:approve",
    async (int articleId, ApproveArticleCommand command, ISender sender, CancellationToken ct) =>
    {
        var response = await sender.Send(command with { ArticleId = articleId }, ct);
        return Results.Ok(response);
    });

// (FastEndpoints equivalent — this is what Auth/Journals already do, and it works
//  because FastEndpoints defines CancellationToken as a magic bound parameter)
public override Task HandleAsync(CreateUserCommand command, CancellationToken ct)

// 2. HANDLER: forward into every await, don't just name it
public async Task<IdResponse> Handle(ApproveArticleCommand command, CancellationToken ct)
{
    var article = await _articleRepository.FindByIdOrThrowAsync(command.ArticleId, ct);
    var editor  = await _personRepository.GetByUserIdAsync(command.CreatedById, ct);
    await _articleRepository.SaveChangesAsync(ct);
    return new IdResponse(article.Id);
}

// 3. REPOSITORY: accept it, default it, forward it
public virtual Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default)
    => Query().SingleOrDefaultAsync(e => e.Id == id, ct);

// 4. EF Core: the terminal hop — this is where the token finally cancels a real
//    DB command. Everything above it is bookkeeping; this is the payload.
```

A pipeline behavior like `ValidationBehavior` also receives `cancellationToken` and already forwards it into `ValidateAsync` — nothing to change there.

**Rule of thumb: `ct = default` on repository/service interface methods in BuildingBlocks; no default on application handlers.** Why the split? The MediatR contract already guarantees a token is supplied, so a default there buys nothing and hides a mistake. Library-style methods do get called from startup jobs, seeders, tests and design-time factories, where there is genuinely no request — `= default` is the deliberate escape hatch (and in `CancellationToken`, `default` *is* `CancellationToken.None`).

### The pattern to actually use: controller / service layer

Same shape, one extra hop. The framework hands you the token at the edge:

| Layer | How the token arrives |
|---|---|
| MVC / minimal API | add `CancellationToken ct` param → bound to `HttpContext.RequestAborted` |
| FastEndpoints | `HandleAsync(..., CancellationToken ct)` |
| MediatR handler | `Handle(TRequest, CancellationToken)` |
| **gRPC server** | `ServerCallContext.CancellationToken` (in this repo: `CallContext` code-first, `context.CancellationToken`) |
| **MassTransit consumer** | `ConsumeContext.CancellationToken` |
| `HttpClient` | pass as last arg to `SendAsync`/`GetAsync`/`PostAsJsonAsync` |
| `IHostedService` / background | link the host's `IHostApplicationLifetime.ApplicationStopping` token with your own |
| EF Core | last arg to `SingleOrDefaultAsync`, `SaveChangesAsync`, `BeginTransactionAsync`, `ToListAsync` |

For a classic service layer, the honest answer is **yes, you drill it** — controller action → application service → repository → EF. The drill is two or three parameters wide, not twenty, because you're not passing it through UI component layers. Where a layer genuinely doesn't need it (pure computation), **stop passing it** — don't thread `ct` into a domain method that only does arithmetic. The token belongs only on methods that do I/O or loop.

### Enterprise-level rules that actually get followed

1. **Let the transport give you the token at the edge; never create one in the middle of a request.** You only create a `CancellationTokenSource` for a *budget* (timeout) or to *link* two tokens.
2. **Forward it into every I/O call.** The single highest-value habit. One dropped hop disables cancellation for everything below it.
3. **Enforce it mechanically, not by discipline.** `<AnalysisMode>All</AnalysisMode>` plus treating `CA2016` as an error turns "did you forward it?" into a build failure. This is what keeps it correct at scale — review attention is not a mechanism.
4. **Don't cancel non-idempotent work blindly.** If the token fires mid-transaction, the transaction must be tied to the token (see `BeginTransactionAsync(ct)`) or earlier writes stand. `OperationCanceledException` bubbled to ASP.NET Core becomes **499**, not 500 — it's "client went away", not "server broke".
5. **Never make the token a constructor dependency.** If you find yourself wanting to, the real problem is usually that the method has too many callers to update — fix that with `= default` on the interface instead.
6. **Don't swallow it.** Catch `OperationCanceledException` only to clean up, then rethrow.

### Where the current project stands (checked against the finished project)

The prop drilling here is currently **broken, not merely verbose** — and interestingly, the finished project doesn't fix it either:

- `ApproveArticleCommandHandler.cs:14` receives `ct`, then drops it at `:16` (`FindByIdOrThrowAsync(command.ArticleId)`), `:18` (gRPC `IsEditorAssignedToJournal`), and `:25` (`SaveChangesAsync()` → the `ct = default` overload → `CancellationToken.None`). Only `:21` forwards it.
- The **finished** `ApproveArticleCommandHandler.cs` is the same: `Handle(..., CancellationToken ct)` arrives, `FindByIdOrThrowAsync(command.ArticleId)` and `SaveChangesAsync()` both take no token. So this is the course's own level of rigor, not a mistake you introduced.
- `FindByIdOrThrowAsync` **has no `ct` parameter in the finished project either** (`Blocks.EntityFrameworkCore/Extensions/RepositoryExtensions.cs:10`), because it calls `FindAsync(id)`. The finished `RepositoryBase` *does* add `ct = default` to `GetByIdAsync`, `AddAsync` and `ExistsAsync` — but `FindByIdAsync` still has none. Meanwhile `GetByIdOrThrowAsync` next to it *does* take `ct = default` (and then doesn't forward it to `GetByIdAsync`). Mixed, even in the reference code.
- **No Submission endpoint passes a token.** Every file in `Submission.Api/Endpoints/` uses `sender.Send(command)` with no `ct` param and no `RequestAborted` — `ApproveArticleEndpoint.cs:11` included. Auth/Journals FastEndpoints do respect `ct`. So today the chain is uncancellable from the very first hop in Submission.
- Repo-wide forwarding is inconsistent: `SubmitArticleCommandHandler` threads `ct` correctly; `AssignAuthorCommandHandler` and `UploadManuscriptFileCommandHandler` name `ct` and never use it.
- `_stateMachineFactory` is referenced at `:23` but isn't in the primary constructor (`:8-11`) — mid-refactor, unrelated to cancellation. `IsEditorAssignedToJournal(article.JournalId, command.CreatedById)` at `:18` is fine; the second argument really is a user id.

Cheapest real improvement, in order: add `CancellationToken ct` to the endpoint lambdas and pass it to `sender.Send(command, ct)`; add `ct = default` to `IRepository<TEntity>` (`Repository.cs:9-15`, including `AddAsync`), which keeps every existing call site compiling; then sweep handlers and forward it. Verify `FindAsync(id, ct)` binds on your EF Core 10.0.12 — EF added a token overload for `FindAsync` in 9, so `=(await _entity.FindAsync(id, ct))` should compile, but that's the one call I'd check before trusting.

## gRPC contracts at scale: publishing, consuming, and the architectural choices

**The scenario:** a .NET-only backend (the "Klarna / Riverty" shape — many services, independent deploys), where services must expose gRPC contracts. How are contracts published, how do consuming services get them, and what does a consumer actually generate?

**The one-line answer:** the contract is a **separately versioned, separately published artifact** — a NuGet package containing interfaces and DTOs. Consuming services add a `PackageReference`, DI wires the client, and **the client + C# types are generated at runtime from the shared interface** — not at compile time, and not by `protoc`.

### Step 0: what this repo does today, and the trap in it

Two contract mechanisms sit side by side in `BuildingBlocks/Articles.Grpc.Contracts/`:

| File | Mechanism | Live? |
|---|---|---|
| `Auth/PersonContracts.cs`, `Journals/JournalContracts.cs` | **code-first** — `[ServiceContract]`/`[OperationContract]` interface + `[ProtoContract]`/`[ProtoMember]` DTOs | **yes** — this is the wire contract |
| `Auth/PersonContracts.proto`, `Journals/JournalContracts.proto` | schema-first `.proto` | **no** — dead files |

**The trap: the `.proto` files do nothing.** No csproj has `<Protobuf Include="..."/>`, there is no `Grpc.Tools` package anywhere in the solution, and `Articles.Grpc.Contracts.csproj` references only `protobuf-net.Grpc` (no `Grpc.Tools`). Nothing ever compiles those protos — they're documentation-shaped files that happen to be git-tracked. Delete them or wire them up; right now they look authoritative and aren't.

What's actually live is **`protobuf-net.Grpc` code-first**: the C# interface *is* the schema. `Auth.Grpc.IPersonService` is implemented server-side by `PersonGrpcService` (`Auth.API/Program.cs:32` → `app.MapGrpcService<PersonGrpcService>()`) and consumed client-side by Journals. There is no codegen step anywhere.

### How the client actually gets generated (this repo, and at scale)

`Blocks.AspNetCore/Grpc/GrpcClientRegistrationExtensions.cs:24-38`:

```csharp
services.AddScoped(sp =>
{
    var channel = GrpcChannel.ForAddress(serviceSettings.Url, new GrpcChannelOptions { ... });
    return channel.CreateGrpcService<TClient>();   // ← magic here
});
```

`CreateGrpcService<TClient>()` is the heart of code-first. `TClient` is `IPersonService` — an **interface with no implementation anywhere**. The result:

| Question | Answer |
|---|---|
| Does the consumer generate a client class? | **Yes — but at runtime.** `protobuf-net.Grpc` uses **Castle DynamicProxy** to emit an implementation of `IPersonService` that serializes each call and sends it over the channel. No `.cs` file, no MSBuild step, no `protoc`. |
| Where do the C# DTO classes come from? | **Straight from the contract package** — `PersonInfo`, `GetPersonResponse` etc. They're already-compiled types in `Articles.Grpc.Contracts.dll`. Nothing is generated for them. |
| Is there a compile-time stub? | **No.** Which is why nothing in Submission knows the URL at compile time — `GrpcServicesOptions` supplies it at startup (`Journals.API/DependencyInjection.cs:49` → `AddCodeFirstGrpcClient<IPersonService>(grpcOptions, "Person")`). |
| Where's the contract *definition*? | In the package. The proxy is a consequence of it. |

This is the architectural payoff of code-first and it's worth being precise about: **the client is generated at runtime from the interface you share.** Compare with schema-first, where `Grpc.Tools` would generate both a client base class and DTOs as source at build time. Two genuinely different models — and the reason a code-first contract package is a *much* smaller thing to publish (no tooling requirement for consumers).

### The best approach for an enterprise .NET-only org

The shape I'd build:

```
contracts/                          ← its own repo, or a folder with its own release cadence
  Articles.Contracts.Auth/          ← one package per service boundary, or one per domain
    IPersonService.cs
    PersonInfo.cs ...
    Articles.Contracts.Auth.csproj  ← Pack=true, Version=..., protobuf-net.Grpc only
  → CI: build, version, push to internal NuGet feed (Azure Artifacts / Artifactory / GitHub Packages)
```

and each consumer:

```xml
<PackageReference Include="Articles.Contracts.Auth" Version="2.4.0" />
```

Six decisions that matter:

1. **Contracts live outside the services — in their own repo with their own version and release cadence.** A service must be able to deploy against contract 2.4 while the producer is already on 2.6. If the contract is a `ProjectReference`, they ship together and that independence is lost. **This is the single most important structural point.**
2. **One package per service boundary, not one giant `Articles.Contracts`.** Consumers should not pull Auth's contracts to talk to Journals. It's also what makes versioning tractable — a fat package means every minor Auth change bumps everyone.
3. **The package contains interfaces + DTOs only. No implementations.** It can be referenced by both sides. Put shared *behaviour* in it and you've recreated a shared kernel across services — that's the anti-pattern. A contract package is a schema; that's the legitimate form of sharing.
4. **Query contracts and message contracts are different packages.** gRPC interfaces → this package. MassTransit integration events → `Articles.IntegrationEvents.Contracts`, published the same way. Different transport, different compatibility rules.
5. **Version with SemVer and mean it:**
   - add optional field / add method → **minor**
   - remove field, renumber a field, change a type, add a *required* field → **major** (these are wire breaks)
   - Never reuse or renumber a `[ProtoMember(n)]`. That number is the wire identity; renumbering silently corrupts data between mismatched versions. Retire numbers instead of recycling them.
6. **Since it's all .NET, code-first is a legitimate deliberate choice** — no `protoc`, no generator versions to align across teams, no generated-code drift, rename-safe refactors. The moment a non-.NET consumer appears (a JVM service, a Go data pipeline), you must switch to `.proto` as the source of truth, because there is no language-neutral schema in C# attributes. That's the trade, and it's worth writing down: **code-first is the reward for being .NET-only, and the thing you give up when you stop being .NET-only.**

### What a consuming service actually gets — and the architectural notes

Adding the package gives you the interface and DTOs. Then three things are *your* decision, not the package's:

**a) Channel lifetime — the classic trap.** A gRPC channel is expensive to create and is designed to be long-lived and shared; a client/proxy is cheap and per-call is fine. Note what `AddCodeFirstGrpcClient` does at `:24`: creates a **new channel inside a scoped factory** — so one channel per scope (per request, in practice). That's harmless in a course, but in production it means a new HTTP/2 connection per request, TLS handshake included. The file itself flags the right answer in a comment at `:98-99` — there's an `AddCodeFirstGrpcClientAsSingleton` that shares one channel + client for the process, "deliberately not wired by any service." For the enterprise version: **register the channel as a singleton** (or named `HttpClient` via `IHttpClientFactory`), and let the proxy be per-call. This is the most common gRPC performance mistake in .NET codebases.

**b) Resilience has to be gRPC-aware.** The comment at `:43` is a genuinely important insight and worth preserving: **a Polly retry policy at the `HttpClient` layer never sees an `RpcException`** — a gRPC call that fails with `StatusCode.Unavailable` returns HTTP 200 at the transport layer, so HTTP-level retry middleware treats it as a success. Retries must be configured either as a gRPC-native `MethodConfig` `RetryPolicy` on the channel (what `AddCodeFirstGrpcClientWithRetry` at `:44` does) or at the client-call layer. `Status.Unavailable` is the retryable one; `InvalidArgument`/`NotFound`/`PermissionDenied` are not, and retrying them just amplifies load.
   Related: pass a **deadline** on every call (`CallContext`/`CallOptions` deadline) — it's the gRPC equivalent of a timeout, and without it a hung dependency hangs your caller indefinitely.

**c) Boundaries — where the nuance actually lives.** A typed interface in a shared package makes a dependency look like a local method call: `_personClient.GetPersonByUserIdAsync(...)` reads like an in-process call and hides a network hop, a latency, and a failure mode. At scale this becomes *distributed monolith by convenience* — services wired into a synchronous call graph, where Auth being down takes down Submission's approve flow. The guardrails:
   - **gRPC for queries and commands that need an answer now; events (MassTransit) for state propagation.** If Submission only needs to *know* a person exists, that's a fact to consume, not a call to make.
   - **Never chain gRPC calls to build a "read model".** Two hops means the product of two availabilities.
   - **Keep the call graph shallow and acyclic.** A contract package makes edges invisible, so enforce direction explicitly (or the graph grows edges nobody intended).
   - **Don't share the contract package between services that shouldn't be coupled.** The package is a dependency edge, whether or not the compiler draws it.

### What you give up vs schema-first (worth knowing before choosing)

| | code-first (`protobuf-net.Grpc`) | schema-first (`Grpc.Tools` + `.proto`) |
|---|---|---|
| Schema language | C# interface + attributes | `.proto` (language-neutral) |
| Consumer codegen | runtime proxy, no build step | build-time generated client + DTOs |
| Non-.NET consumers | **impossible** | first-class |
| Breaking-change tooling | none built in — rely on SemVer discipline | `buf breaking`, protolock, `Buf Schema Registry` |
| Refactoring the contract | ordinary rename, compiler checks call sites | edit `.proto`, regenerate everywhere |
| Consumer requirement | just the package | a working protoc toolchain (unless protos are pre-generated) |

The row I'd underline: **schema-first gets you mechanical breaking-change detection; code-first gets you none.** That's the real cost of code-first, and it's the reason a large polyglot org keeps `.proto` even when the .NET teams would prefer not to. In a .NET-only org you can substitute a `buf`-less equivalent: `[ProtoMember(n)]` numbers are the compatibility contract, so enforce them in review or via a Roslyn analyzer / API-compat tool (`Microsoft.CodeAnalysis.PublicApiAnalyzers`) against the published package.

### Checklist if building this for real

- Contracts in their own repo, own version, own release pipeline; pushed to an internal feed.
- One package per service boundary; interfaces + DTOs only, no logic.
- `[ProtoMember]` numbers treated as immutable and never recycled; SemVer enforced by API-compat checks in CI.
- Channel/proxy registered as **singleton** (or via `IHttpClientFactory`), never per-scope in production.
- gRPC-native retries (`Unavailable` only) + a deadline on every call.
- Integration-event contracts published as their own package, separate cadence.
- Call graph kept shallow and acyclic; gRPC only when you need an answer now, events otherwise.
- The day a non-.NET consumer appears, migrate the contract to `.proto` and generate stubs in CI — the C# interface becomes generated output, not the source.

## Aggregates and Aggregate Roots: what they are, and where this project draws the boundaries

**File references (current project):**
- The aggregate root candidate #1 — entity: `src/Articles/Services/Submission/Submission.Domain/Entities/Journal.cs`
- Its behavior: `src/Articles/Services/Submission/Submission.Domain/Behaviors/Journal.cs` (`CreateArticle`)
- The aggregate root #2 — entity: `src/Articles/Services/Submission/Submission.Domain/Entities/Article.cs`
- Its behavior: `src/Articles/Services/Submission/Submission.Domain/Behaviors/Article.cs` (`CreateAsset`, `Submit`, `Approve`)
- Handlers that *use* aggregates instead of bypassing them:
  - `src/Articles/Services/Submission/Submission.Application/Features/CreateArticle/CreateArticleCommandHandler.cs` (`journal.CreateArticle(...)`)
  - `.../Features/ApproveArticle/ApproveArticleCommandHandler.cs` (loads `article` by its own id)
  - `.../Features/SubmitArticle/SubmitArticleCommandHandler.cs` (`article.Submit(command, _stateMachineFactory)`)

**File references (finished project — where the pattern gets its machinery):**
- `finished/src/BuildingBlocks/Blocks.Domain/Entities/AggregateRoot.cs` — `IAggregateRoot`, the domain-events collection
- `finished/src/Services/Submission/Submission.Domain/Behaviors/Article.cs` — `SetStage` + `AddDomainEvent(new ArticleApproved(...))`
- `finished/src/Services/Submission/Submission.Domain/IArticleStateMachine.cs` — the state machine the root uses to guard stage transitions

### What an aggregate is

An **aggregate** is a cluster of entities and value objects that must stay **consistent as a single unit**. Three parts to the idea:

| Part | What it is | Rule it imposes |
|---|---|---|
| **Root** | One entity others reach in through | All mutations go through its methods |
| **Boundary** | Everything inside the cluster (child entities, collections, VOs) | Outside code never touches children directly |
| **Invariants** | Rules that must hold after *every* change | The root is their only enforcement point |

"Aggregate" and "aggregate root" are often used interchangeably; precisely, the **aggregate** is the whole cluster + boundary, the **root** is the one entity you're allowed to hold a reference to and call. `Article` is the root; an `Asset` inside it is not — you never load an `Asset` by itself and mutate it.

### Why the pattern exists (four reasons, in order of practicality)

1. **Invariants need an owner.** "An article always starts in the `Created` stage", "max 3 manuscript files per asset type", "every actor has a role" — these can only be *guaranteed* if every mutation passes through one place. Scattered logic guarantees nothing.
2. **It is the transaction boundary.** One transaction = one aggregate: load it, mutate it, `SaveChangesAsync`. Either the whole cluster is consistent or nothing is. This is also the line where the domain-events rule from the CQRS note comes from: rules spanning *two* aggregates cannot be enforced in one transaction — they become events (eventual consistency).
3. **The write side gets one door.** Queries may project and read freely (`AsNoTracking`, DTO projections), but every *write* enters through the root. That asymmetry is what keeps the model honest.
4. **In microservices, the aggregate is what a service fully owns.** "One database per service" and "reference other services' data by id only" are aggregate thinking applied at service scale.

### The two aggregates in this project

**`Journal` — root.** Its entity (`Entities/Journal.cs`) holds state; its behavior file (`Behaviors/Journal.cs`) holds the rules. Split is deliberate:

```csharp
// Behaviors/Journal.cs
public Article CreateArticle(string title, ArticleType Type, string scope)
{
    var article = new Article()
    {
        Title = title, Type = Type, Scope = scope,
        Journal = this,                // ← invariant: an article is never journal-less
        Stage = ArticleStage.Created   // ← invariant: the only valid birth stage
    };
    _articles.Add(article);
    return article;
}
```

If this were `new Article { JournalId = command.JournalId }` in the handler, nothing would stop a detached article or a bogus stage. Inside the root, both mistakes are *unrepresentable*.

**`Article` — root.** `CreateAsset` is the cleanest proof of why the boundary matters:

```csharp
// Behaviors/Article.cs
public Asset CreateAsset(AssetTypeDefinition type)
{
    var assetCount = _assets.Where(a => a.AssetType == type.Id).Count();
    if (assetCount >= type.MaxAssetCount)
        throw new DomainException(...);
    var asset = Asset.Create(this, type);
    _assets.Add(asset);
    return asset;
}
```

The max-count check is only correct because **all assets live inside the boundary** — counting `_assets` is guaranteed complete. If assets were managed through a separate repository, two handlers racing could both pass the check. The boundary is what turns a "usually correct" check into a real invariant.

`Stage` has `internal set` (`Entities/Article.cs:14`) — the lifecycle is only drivable from inside the domain assembly. The root guards its own state machine.

### The subtlety: `Article` is born a child of `Journal`, then becomes its own root

Not sloppy — a modeling decision the code makes explicit:

- `Article.JournalId` is `init` (`Entities/Article.cs:15`) — a reference to *another aggregate by identity*, set once, never re-pointed.
- `ApproveArticleCommandHandler` loads `article` directly via `FindByIdOrThrowAsync(command.ArticleId)` — never via `journal.Articles`. An entity that is **fetched by its own id and mutated by its own commands** is a root, even if another aggregate created it.
- Journal's invariant job ends at creation: "every article I create is well-formed and mine."

**Rule of thumb:** if callers address it by id and send it commands, it's a root. Creation-time parentage doesn't matter.

### What is *not* an aggregate here

- `Person` (in Submission) — a locally-synced copy of data owned by the Auth service. Reference data, not a guarded boundary. Same for `AssetTypeDefinition`.
- Value objects (`FileName`, `EmailAddress`) — no identity, no lifecycle; typed values *inside* a boundary.
- `ArticleActor` — exists only as a child of `Article`; it is part of the boundary, never addressed alone.

### How the code enforces the boundary (mechanics, not vibes)

| Mechanic | File | What it prevents |
|---|---|---|
| `private List<T> _assets` + `IReadOnlyList<T> Assets` | `Entities/Article.cs:18-19`, `Entities/Journal.cs` | Callers mutating children without a rule check (see the encapsulation note above — same pattern as `User.UserRoles`) |
| `internal set` on `Stage` | `Entities/Article.cs:14` | Handlers/other assemblies driving the lifecycle directly |
| Partial class split: `Entities/` = state, `Behaviors/` = rules | both files | Rule logic leaking into handlers ("no service layer classes" guardrail) |
| Root methods take the command (`action`) | `Behaviors/Article.cs` (`Submit(action, factory)`) | The aggregate reads `action.CreatedById`/`CreatedOn` — audit data flows *in*, never out |

### The finished project's evolution: `AggregateRoot` + domain events

The finished project formalizes the root with a base class — worth knowing what's coming:

```csharp
// finished: Blocks.Domain/Entities/AggregateRoot.cs
public interface IAggregateRoot<TPrimaryKey> : IAuditedEntity<TPrimaryKey>
{
    public IReadOnlyList<IDomainEvent> DomainEvents { get; }
    public void AddDomainEvent(IDomainEvent eventItem);
    public void ClearDomainEvents();
}
```

Note the shape is the *same encapsulation trick* as `User.UserRoles`: a private `List<IDomainEvent>`, exposed as `IReadOnlyList`. The aggregate **collects** events; it doesn't publish them. Dispatch happens later (an EF Core `SaveChangesInterceptor` — see the CQRS note's last section), so the root stays ignorant of everything outside its boundary. That separation is the point: the root raises facts, infrastructure decides delivery.

And the behavior side wires it together — root method = guard the transition + mutate + record the fact:

```csharp
// finished: Behaviors/Article.cs
public void Approve(Person editor, IArticleAction<ArticleActionType> action, ArticleStateMachineFactory _stateMachineFactory)
{
    _actors.Add(new ArticleActor { Person = editor, Role = UserRoleType.REVED });
    SetStage(ArticleStage.InitialApproved, action, _stateMachineFactory);   // validates via the state machine
    AddDomainEvent(new ArticleApproved(this, action));
}
```

Also note the finished `Article : AggregateRoot` vs `Journal : Entity` — only the root gets the audited-properties + domain-events machinery, because (as the code comment says) child entities are saved *as part of* an aggregate and inherit its audit values.

### Rules to apply when adding a new feature

1. **One transaction = one aggregate.** A handler may load and mutate exactly one root before `SaveChangesAsync`.
2. **Reference other aggregates by id** (`JournalId` as `int`, `init`) — not by object navigation across the boundary.
3. **New mutation = new method on the root**, in `Behaviors/`. Never `article.Assets.Add(...)` from a handler — the compiler already forbids it (`IReadOnlyList`); don't defeat it.
4. **Cross-aggregate reactions become events**, not extra repository calls inside the same handler.
5. **Roots get `AggregateRoot` (or at least audited fields + event collection); children stay plain `Entity`.**

### How to find the boundary yourself (the skill that generalizes)

Ask three questions about any candidate entity:

1. **What must be true after every transaction?** → the answers are the invariants; everything needed to enforce them is *inside* the boundary.
2. **What gets fetched by its own id and commanded directly?** → roots.
3. **What merely points at other things?** → reference by id, not membership.

For `CreateAsset`, question 1 alone proves `Asset` belongs inside `Article`. For `Journal`/`Article`, question 2 is what splits them into two aggregates.

Honest footnote: the current `Article` keeps both `JournalId` *and* a `Journal` navigation property. Strict DDD says id-only references across aggregate boundaries; the navigation is an EF convenience that bends the rule. Common and pragmatic — just remember the **id is the real link**, the navigation is query sugar.

using Microsoft.Extensions.Caching.Memory;

namespace Review.Persistence;

public partial class ReviewDbContext(DbContextOptions<ReviewDbContext> options, IMemoryCache cache)
    : DbContext(options)
{
    #region Entities

    public virtual DbSet<Article> Articles { get; set; }
    public virtual DbSet<ArticleActor> ArticleActors { get; set; }
    public virtual DbSet<Asset> Assets { get; set; }
    public virtual DbSet<AssetTypeDefinition> AssetTypes { get; set; }

    public virtual DbSet<Journal> Journals { get; set; }

    // public virtual DbSet<Stage> Stages { get; set; }
    // public virtual DbSet<StageHistory> StageHistories { get; set; }
    public virtual DbSet<Person> Persons { get; set; }
    public virtual DbSet<Author> Authors { get; set; }
    public virtual DbSet<Reviewer> Reviewers { get; set; }
    public virtual DbSet<Editor> Editors { get; set; }
    // public virtual DbSet<ReviewInvitation> ReviewInvitations { get; set; }

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Tell EF to use all the configurations defined in EntityConfigurations folder
        modelBuilder.ApplyConfigurationsFromAssembly(this.GetType().Assembly);

        // modelBuilder.UseEntityTypeNamesAsTables();
    }

    // public async override Task<int> SaveChangesAsync(CancellationToken ct = default)
    // {
    //     Debug.WriteLine(string.Join("\n",
    //         base.ChangeTracker.Entries().Select(e => $"{e.Entity.GetType().Name} -> {e.State}")));
    //
    //     this.UnTrackCacheableEntities();
    //
    //     return await base.SaveChangesAsync(ct);
    // }
}
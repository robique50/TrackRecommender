using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore;
using TrackRecommender.Server.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
    : base(options) { }

    public DbSet<Trail> Trails { get; set; }
    public DbSet<Region> Regions { get; set; }
    public DbSet<TrailRegion> TrailRegions { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserPreferences> UserPreferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var stringListConverter = new ValueConverter<List<string>, string>(
            v => v == null || !v.Any() ? string.Empty : string.Join(";", v),
            v => string.IsNullOrWhiteSpace(v) ? new List<string>() : new List<string>(v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)));

        var stringListComparer = new ValueComparer<List<string>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c == null ? new List<string>() : c.ToList());

        modelBuilder.Entity<Trail>(entity =>
        {
            entity.Property(t => t.Tags)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            entity.HasMany(t => t.Regions)
                  .WithMany(r => r.Trails)
                  .UsingEntity<TrailRegion>(
                      j => j.HasOne(tr => tr.Region)
                            .WithMany()
                            .HasForeignKey(tr => tr.RegionId),
                      j => j.HasOne(tr => tr.Trail)
                            .WithMany()
                            .HasForeignKey(tr => tr.TrailId),
                      j => j.HasKey(tr => new { tr.TrailId, tr.RegionId })
                  );
        });

        modelBuilder.Entity<UserPreferences>(entity =>
        {
            entity.HasOne(up => up.User)
                .WithOne(u => u.Preferences)
                .HasForeignKey<UserPreferences>(up => up.UserId)
                .IsRequired();
        });
    }
}
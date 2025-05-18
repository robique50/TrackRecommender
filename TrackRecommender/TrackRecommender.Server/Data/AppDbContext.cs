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
    public DbSet<UserTrailRating> UserTrailRatings { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

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

        var intListConverter = new ValueConverter<List<int>, string>(
            v => v == null || !v.Any() ? string.Empty : string.Join(";", v),
            v => string.IsNullOrWhiteSpace(v) ? new List<int>() : v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                               .Select(int.Parse)
                                                               .ToList());

        var intListComparer = new ValueComparer<List<int>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c == null ? new List<int>() : c.ToList());

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

            entity.HasMany(t => t.UserRatings)
                  .WithOne(ur => ur.Trail)
                  .HasForeignKey(ur => ur.TrailId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPreferences>(entity =>
        {
            entity.HasOne(up => up.User)
                .WithOne(u => u.Preferences)
                .HasForeignKey<UserPreferences>(up => up.UserId)
                .IsRequired();

            entity.Property(up => up.PreferredTrailTypes)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            entity.Property(up => up.PreferredTags)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            entity.Property(up => up.PreferredCategories)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            entity.Property(up => up.PreferredRegionIds)
                .HasConversion(intListConverter)
                .Metadata.SetValueComparer(intListComparer);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();

            entity.HasMany(u => u.TrailRatings)
                  .WithOne(tr => tr.User)
                  .HasForeignKey(tr => tr.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserTrailRating>(entity =>
        {
            entity.HasKey(ur => ur.Id);

            entity.HasIndex(ur => new { ur.UserId, ur.TrailId }).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
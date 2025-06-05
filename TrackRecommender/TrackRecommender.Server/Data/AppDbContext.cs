using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using TrackRecommender.Server.Models;

namespace TrackRecommender.Server.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Trail> Trails { get; set; }
        public DbSet<Region> Regions { get; set; }
        public DbSet<TrailRegion> TrailRegions { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserPreferences> UserPreferences { get; set; }
        public DbSet<UserTrailRating> UserTrailRatings { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        private static readonly char[] separator = [';'];

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var jsonStringListConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>()
            );

            var stringListComparer = new ValueComparer<List<string>>(
                (c1, c2) => c1 == null && c2 == null || c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? new List<string>() : c.ToList()
            );

            var intListConverter = new ValueConverter<List<int>, string>(
                v => v == null || !v.Any() ? string.Empty : string.Join(";", v),
                v => string.IsNullOrWhiteSpace(v) ? new List<int>() : v.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                                                                    .Select(int.Parse)
                                                                    .ToList()
            );

            var intListComparer = new ValueComparer<List<int>>(
                (c1, c2) => c1 == null && c2 == null || c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c == null ? new List<int>() : c.ToList()
            );

            modelBuilder.Entity<Trail>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.HasIndex(t => t.OsmId).IsUnique();

                entity.Property(t => t.Coordinates)
                      .HasColumnType("geometry")
                      .HasAnnotation("Srid", 4326)
                      .IsRequired();

                entity.Property(t => t.Tags)
                      .HasConversion(jsonStringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);
            });

            modelBuilder.Entity<Region>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.HasIndex(r => r.Name).IsUnique();

                entity.Property(r => r.Boundary)
                      .HasColumnType("geometry")
                      .HasAnnotation("Srid", 4326)
                      .IsRequired(false);
            });

            modelBuilder.Entity<TrailRegion>(entity =>
            {
                entity.HasKey(tr => new { tr.TrailId, tr.RegionId });

                entity.HasOne(tr => tr.Trail)
                      .WithMany(t => t.TrailRegions)
                      .HasForeignKey(tr => tr.TrailId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(tr => tr.Region)
                      .WithMany(r => r.Trails)
                      .HasForeignKey(tr => tr.RegionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<UserPreferences>(entity =>
            {
                entity.HasKey(up => up.Id);
                entity.HasOne(up => up.User)
                      .WithOne(u => u.Preferences)
                      .HasForeignKey<UserPreferences>(up => up.UserId)
                      .IsRequired();

                entity.Property(up => up.PreferredTrailTypes)
                      .HasConversion(jsonStringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);

                entity.Property(up => up.PreferredTags)
                      .HasConversion(jsonStringListConverter)
                      .Metadata.SetValueComparer(stringListComparer);

                entity.Property(up => up.PreferredCategories)
                      .HasConversion(jsonStringListConverter)
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
}
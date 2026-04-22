using Microsoft.EntityFrameworkCore;
using StreamHandler.API.Models;

namespace StreamHandler.API.Data;

public class StreamDbContext : DbContext
{
    public StreamDbContext(DbContextOptions<StreamDbContext> options) : base(options) { }

    public DbSet<Models.Stream> Streams => Set<Models.Stream>();
    public DbSet<StreamEvent> StreamEvents => Set<StreamEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.Stream>(e =>
        {
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.Url).IsUnique();
            e.HasMany(s => s.Events)
             .WithOne(ev => ev.Stream)
             .HasForeignKey(ev => ev.StreamId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StreamEvent>(e =>
        {
            e.HasIndex(ev => ev.StreamId);
            e.HasIndex(ev => ev.OccurredAt);
        });
    }
}

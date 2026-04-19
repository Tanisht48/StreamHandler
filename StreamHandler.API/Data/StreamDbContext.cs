using Microsoft.EntityFrameworkCore;
using StreamHandler.API.Models;

namespace StreamHandler.API.Data;

public class StreamDbContext : DbContext
{
    public StreamDbContext(DbContextOptions<StreamDbContext> options) : base(options) { }

    public DbSet<Models.Stream> Streams => Set<Models.Stream>();
}

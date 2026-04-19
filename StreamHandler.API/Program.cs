using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.EntityFrameworkCore;
using StreamHandler.API.Data;
using StreamHandler.API.Hubs;
using StreamHandler.API.Jobs;
using StreamHandler.API.Models;
using StreamHandler.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── CORS (allow Angular dev server) ───────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()); // required for SignalR WebSockets
});

// ── SignalR ────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// ── Database: PostgreSQL if connection string present, else InMemory ───────
var connStr = builder.Configuration.GetConnectionString("StreamHandlerDb");
if (!string.IsNullOrWhiteSpace(connStr))
{
    builder.Services.AddDbContext<StreamDbContext>(options =>
        options.UseNpgsql(connStr));
    Console.WriteLine("[DB] Using PostgreSQL");
}
else
{
    builder.Services.AddDbContext<StreamDbContext>(options =>
        options.UseInMemoryDatabase("StreamHandlerDb"));
    Console.WriteLine("[DB] PostgreSQL connection string not found — using InMemory database");
}

// ── HTTP client for health checks ─────────────────────────────────────────
builder.Services.AddHttpClient("HealthCheck", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("StreamHandler/1.0");
});

// ── Application services ───────────────────────────────────────────────────
builder.Services.AddScoped<StreamFormatDetector>();
builder.Services.AddScoped<StreamHealthService>();
builder.Services.AddScoped<StreamService>();
builder.Services.AddScoped<StreamHealthJob>();

// ── Hangfire ──────────────────────────────────────────────────────────────
builder.Services.AddHangfire(config => config.UseMemoryStorage());
builder.Services.AddHangfireServer(options => { options.WorkerCount = 2; });

var app = builder.Build();

// ── Run EF migrations if PostgreSQL, seed if InMemory ─────────────────────
await InitializeDatabaseAsync(app);

// ── Middleware pipeline ────────────────────────────────────────────────────
app.UseCors("Angular");
app.UseHangfireDashboard("/hangfire");
app.MapControllers();
app.MapHub<StreamStatusHub>("/hubs/stream-status");

// ── Register recurring Hangfire job ──────────────────────────────────────
var checkInterval = app.Configuration.GetValue<int>("StreamHealth:CheckIntervalMinutes", 2);
RecurringJob.AddOrUpdate<StreamHealthJob>(
    "stream-health-check",
    job => job.RunAsync(JobCancellationToken.Null),
    $"*/{checkInterval} * * * *");

app.Run();

// ── Database init ─────────────────────────────────────────────────────────
static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db     = scope.ServiceProvider.GetRequiredService<StreamDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var isRelational = db.Database.IsRelational();

    if (isRelational)
    {
        logger.LogInformation("Applying EF migrations...");
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    // Seed only when the table is empty
    if (!db.Streams.Any())
        await SeedStreamsAsync(db, scope.ServiceProvider, logger);
}

static async Task SeedStreamsAsync(StreamDbContext db, IServiceProvider sp, ILogger logger)
{
    var detector = sp.GetRequiredService<StreamFormatDetector>();

    var seeds = new[]
    {
        ("http://stream.live.vc.bbcmedia.co.uk/bbc_world_service", "BBC World Service"),
        ("http://icecast.omroep.nl/radio1-bb-mp3",                 "Dutch Radio 1"),
        ("https://stream.radioparadise.com/aac-128",               "Radio Paradise")
    };

    foreach (var (url, name) in seeds)
    {
        db.Streams.Add(new StreamHandler.API.Models.Stream
        {
            Url           = url,
            Name          = name,
            Format        = detector.Detect(url),
            Status        = StreamStatus.Unknown,
            AddedAt       = DateTime.UtcNow,
            LastCheckedAt = DateTime.UtcNow
        });
        logger.LogInformation("Seeded stream '{Name}'", name);
    }

    await db.SaveChangesAsync();
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StreamHandler.API.Data;
using StreamHandler.API.Models;

namespace StreamHandler.API.Controllers;

[ApiController]
[Route("status")]
public class StatusController : ControllerBase
{
    private readonly StreamDbContext _db;

    public StatusController(StreamDbContext db)
    {
        _db = db;
    }

    // GET /status
    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var streams = await _db.Streams.ToListAsync();

        var result = new
        {
            TotalStreams = streams.Count,
            Live = streams.Count(s => s.Status == StreamStatus.Live),
            Offline = streams.Count(s => s.Status == StreamStatus.Offline),
            Checking = streams.Count(s => s.Status == StreamStatus.Checking),
            Unknown = streams.Count(s => s.Status == StreamStatus.Unknown),
            LastHealthCheckRanAt = streams.Count > 0
                ? streams.Max(s => s.LastCheckedAt)
                : (DateTime?)null,
            ServerTimeUtc = DateTime.UtcNow
        };

        return Ok(result);
    }
}

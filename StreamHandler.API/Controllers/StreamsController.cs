using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StreamHandler.API.Data;
using StreamHandler.API.Models;
using StreamHandler.API.Services;

namespace StreamHandler.API.Controllers;

[ApiController]
[Route("streams")]
public class StreamsController : ControllerBase
{
    private readonly StreamService _streamService;
    private readonly StreamHealthService _healthService;
    private readonly StreamDbContext _db;

    public StreamsController(StreamService streamService, StreamHealthService healthService, StreamDbContext db)
    {
        _streamService = streamService;
        _healthService = healthService;
        _db = db;
    }

    // POST /streams
    [HttpPost]
    public async Task<IActionResult> AddStream([FromBody] AddStreamRequest request)
    {
        var stream = await _streamService.AddStreamAsync(request);
        var response = MapToResponse(stream);
        return CreatedAtAction(nameof(GetStream), new { id = stream.Id }, response);
    }

    // GET /streams
    [HttpGet]
    public async Task<IActionResult> GetStreams()
    {
        var streams = await _streamService.GetAllStreamsAsync();
        return Ok(streams.Select(MapToResponse));
    }

    // GET /streams/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStream(Guid id)
    {
        var stream = await _streamService.GetStreamByIdAsync(id);
        if (stream is null) return NotFound();
        return Ok(MapToResponse(stream));
    }

    // DELETE /streams/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStream(Guid id)
    {
        var deleted = await _streamService.DeleteStreamAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }

    // POST /streams/{id}/check
    [HttpPost("{id:guid}/check")]
    public async Task<IActionResult> TriggerHealthCheck(Guid id)
    {
        var stream = await _streamService.GetStreamByIdAsync(id);
        if (stream is null) return NotFound();

        await _healthService.CheckStreamHealthAsync(id);

        var updated = await _streamService.GetStreamByIdAsync(id);
        return Ok(MapToResponse(updated!));
    }

    // GET /streams/{id}/history
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetStreamHistory(Guid id, [FromQuery] int limit = 50)
    {
        var exists = await _db.Streams.AnyAsync(s => s.Id == id);
        if (!exists) return NotFound();

        var events = await _db.StreamEvents
            .Where(e => e.StreamId == id)
            .OrderByDescending(e => e.OccurredAt)
            .Take(Math.Min(limit, 200))
            .Select(e => new StreamEventResponse
            {
                Id         = e.Id,
                OldStatus  = e.OldStatus.ToString(),
                NewStatus  = e.NewStatus.ToString(),
                OccurredAt = e.OccurredAt
            })
            .ToListAsync();

        // Calculate uptime % from all Live events / total events
        var allEvents = await _db.StreamEvents
            .Where(e => e.StreamId == id)
            .ToListAsync();

        double uptimePct = 0;
        if (allEvents.Count > 0)
        {
            var liveCount = allEvents.Count(e => e.NewStatus == StreamStatus.Live);
            uptimePct = Math.Round((double)liveCount / allEvents.Count * 100, 1);
        }

        return Ok(new { UptimePercent = uptimePct, Events = events });
    }

    private static StreamStatusResponse MapToResponse(Models.Stream s) => new()
    {
        Id            = s.Id,
        Name          = s.Name,
        Url           = s.Url,
        Format        = s.Format,
        Status        = s.Status,
        LastCheckedAt = s.LastCheckedAt,
        LastSeenLiveAt = s.LastSeenLiveAt,
        Tags          = s.Tags,
        Codec         = s.Codec,
        Resolution    = s.Resolution,
        BitrateBps    = s.BitrateBps
    };
}

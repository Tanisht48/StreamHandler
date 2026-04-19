using Microsoft.AspNetCore.Mvc;
using StreamHandler.API.Models;
using StreamHandler.API.Services;

namespace StreamHandler.API.Controllers;

[ApiController]
[Route("streams")]
public class StreamsController : ControllerBase
{
    private readonly StreamService _streamService;
    private readonly StreamHealthService _healthService;

    public StreamsController(StreamService streamService, StreamHealthService healthService)
    {
        _streamService = streamService;
        _healthService = healthService;
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

    private static StreamStatusResponse MapToResponse(Models.Stream s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Url = s.Url,
        Format = s.Format,
        Status = s.Status,
        LastCheckedAt = s.LastCheckedAt
    };
}

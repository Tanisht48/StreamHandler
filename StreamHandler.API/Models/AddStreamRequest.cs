using System.ComponentModel.DataAnnotations;

namespace StreamHandler.API.Models;

public class AddStreamRequest
{
    [Required]
    public string Url { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Tags { get; set; }
}

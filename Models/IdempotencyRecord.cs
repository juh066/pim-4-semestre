using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models;

public class IdempotencyRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required, MaxLength(40)] public string Scope { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string RequestId { get; set; } = string.Empty;
    public int? ResultId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

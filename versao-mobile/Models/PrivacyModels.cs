using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models;

public sealed class AccountDeletionRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required, MaxLength(20)] public string Status { get; set; } = DeletionRequestStatuses.Pending;
    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public int? CompletedByAdministratorId { get; set; }
    public User? User { get; set; }
}

public static class DeletionRequestStatuses
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
}

public sealed class AdminAuditLog
{
    public int Id { get; set; }
    public int? AdministratorId { get; set; }
    [Required, MaxLength(80)] public string Action { get; set; } = string.Empty;
    [Required, MaxLength(80)] public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(500)] public string? Reason { get; set; }
    public string? PreviousValues { get; set; }
    public string? NewValues { get; set; }
}

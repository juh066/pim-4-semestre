using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models;

public sealed class DataMigrationIssue
{
    public int Id { get; set; }
    [Required, MaxLength(80)] public string Entity { get; set; } = string.Empty;
    public int LegacyId { get; set; }
    [Required, MaxLength(500)] public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class DataImportHistory
{
    public int Id { get; set; }
    [Required, MaxLength(500)] public string SourcePath { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string SourceSha256 { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Status { get; set; } = DataImportStatuses.Running;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    [MaxLength(2000)] public string? Details { get; set; }
}

public static class DataImportStatuses
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

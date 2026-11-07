using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models;

public class VisitReservation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int VisitSessionId { get; set; }
    [Required, MaxLength(20)] public string Type { get; set; } = ReservationTypes.Ticket;
    [Range(1, 20)] public int Quantity { get; set; }
    [Required, MaxLength(20)] public string Status { get; set; } = ReservationStatuses.Confirmed;
    [Required, MaxLength(64)] public string RequestId { get; set; } = string.Empty;
    [Required, MaxLength(140)] public string ExposureName { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAtUtc { get; set; }
    [MaxLength(20)] public string? CancellationSource { get; set; }
    [MaxLength(500)] public string? CancellationReason { get; set; }
    public User? User { get; set; }
    public VisitSession? VisitSession { get; set; }
}

public static class ReservationTypes
{
    public const string Ticket = "Ticket";
    public const string Appointment = "Appointment";
}

public static class ReservationStatuses
{
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

public static class CancellationSources
{
    public const string Visitor = "Visitor";
    public const string Gallery = "Gallery";
}

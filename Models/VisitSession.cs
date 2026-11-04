using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models;

public class VisitSession
{
    public int Id { get; set; }
    public int ExposureId { get; set; }
    public DateTime VisitDate { get; set; }
    public TimeSpan StartTime { get; set; }
    [Range(1, 10000)] public int Capacity { get; set; }
    public int ReservedQuantity { get; set; }
    public bool IsActive { get; set; } = true;
    public Exposicao? Exposure { get; set; }
    public ICollection<VisitReservation> Reservations { get; set; } = new List<VisitReservation>();
}

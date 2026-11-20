using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models
{
    public class BuyTicketRequest
    {
        [Range(1, 20)]
        public int Quantity { get; set; }

        [Range(1, int.MaxValue)] public int SessionId { get; set; }

        [Required, MaxLength(64)] public string RequestId { get; set; } = string.Empty;
    }

    public class ScheduleAppointmentRequest
    {
        [Range(1, int.MaxValue)] public int SessionId { get; set; }

        [Required, MaxLength(64)] public string RequestId { get; set; } = string.Empty;
    }
}

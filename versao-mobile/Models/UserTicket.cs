using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models
{
    public class UserTicket
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(140)]
        public string EventName { get; set; } = string.Empty;

        public DateTime EventDate { get; set; }

        public int Quantity { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "available";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }
}

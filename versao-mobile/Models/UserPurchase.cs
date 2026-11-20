using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models
{
    public class UserPurchase
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [MaxLength(120)]
        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal TotalPrice { get; set; }

        [Required]
        [MaxLength(40)]
        public string Status { get; set; } = "confirmado";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }
}

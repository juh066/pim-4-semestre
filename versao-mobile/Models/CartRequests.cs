using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models
{
    public class AddCartItemRequest
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(1, 20)]
        public int Quantity { get; set; } = 1;

        [Required, MaxLength(64)]
        public string RequestId { get; set; } = string.Empty;
    }

    public class CheckoutRequest
    {
        [Required, MaxLength(64)]
        public string RequestId { get; set; } = string.Empty;
    }
}

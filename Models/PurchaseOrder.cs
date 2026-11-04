using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models;

public class PurchaseOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    [Required, MaxLength(64)] public string RequestId { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string Status { get; set; } = "Simulated";
    public decimal TotalPrice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}

public class PurchaseItem
{
    public int Id { get; set; }
    public int PurchaseOrderId { get; set; }
    public int? ProductId { get; set; }
    [Required, MaxLength(120)] public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public Produto? Product { get; set; }
}

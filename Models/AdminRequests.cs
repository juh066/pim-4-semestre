using System.ComponentModel.DataAnnotations;

namespace AuroraGaleria.Models;

public class ExposureAdminRequest
{
    public int Id { get; set; }
    [Required, MaxLength(140)] public string Title { get; set; } = string.Empty;
    [MaxLength(1000)] public string Description { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string ImageUrl { get; set; } = string.Empty;
    [Required, MaxLength(80)] public string Period { get; set; } = string.Empty;
    [Required] public DateTime StartDate { get; set; }
    [Required] public DateTime EndDate { get; set; }
}

public class ProductAdminRequest
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = string.Empty;
    [Range(0.01, 999999)] public decimal Price { get; set; }
    [Required, MaxLength(300)] public string ImageUrl { get; set; } = string.Empty;
}

public class SessionAdminRequest
{
    [Range(1, int.MaxValue)] public int ExposureId { get; set; }
    [Required] public DateTime VisitDate { get; set; }
    [Required] public TimeSpan StartTime { get; set; }
    [Range(1, 10000)] public int Capacity { get; set; }
}

public class AdminDashboardViewModel
{
    public IReadOnlyList<Exposicao> Exposures { get; init; } = [];
    public IReadOnlyList<Produto> Products { get; init; } = [];
    public IReadOnlyList<VisitSession> Sessions { get; init; } = [];
}

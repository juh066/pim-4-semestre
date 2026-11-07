namespace AuroraGaleria.Configuration;

public sealed class GalleryOptions
{
    public const string SectionName = "Gallery";
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
}

public sealed class RetentionOptions
{
    public const string SectionName = "Retention";
    public string? FinalEvaluationDate { get; set; }
    public int PersonalDataRetentionDays { get; set; } = 30;
}

namespace AuroraGaleria.Configuration;

public sealed class AcademicDemoOptions
{
    public const string SectionName = "AcademicDemo";

    public bool EnableAdminRegistration { get; set; }

    public string AdminRegistrationCode { get; set; } = string.Empty;
}

using System.Net.Mail;

namespace AuroraGaleria.Services;

public static class EmailAddressValidator
{
    public const int MaximumLength = 256;
    public const string ErrorMessage = "Informe um e-mail válido, como nome@dominio.com.";

    public static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static bool IsValid(string? email)
    {
        var normalized = Normalize(email);
        if (normalized.Length == 0 || normalized.Length > MaximumLength || normalized.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = normalized.IndexOf('@');
        if (at <= 0 || at != normalized.LastIndexOf('@') || at == normalized.Length - 1)
        {
            return false;
        }

        var local = normalized[..at];
        var domain = normalized[(at + 1)..];
        if (local.Length > 64 || local.StartsWith('.') || local.EndsWith('.') || local.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        if (domain.Length > 253 || domain.StartsWith('.') || domain.EndsWith('.') || domain.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var labels = domain.Split('.');
        if (labels.Length < 2 || labels.Any(label =>
                label.Length is 0 or > 63 ||
                label.StartsWith('-') || label.EndsWith('-') ||
                label.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-')))
        {
            return false;
        }

        var ending = labels[^1];
        if (ending.Length < 2 || ending.All(char.IsDigit))
        {
            return false;
        }

        try
        {
            return new MailAddress(normalized).Address.Equals(normalized, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

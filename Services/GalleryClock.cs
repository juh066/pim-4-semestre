using AuroraGaleria.Configuration;
using Microsoft.Extensions.Options;

namespace AuroraGaleria.Services;

public interface IGalleryClock
{
    DateTimeOffset UtcNow { get; }
    DateTimeOffset GalleryNow { get; }
    DateTimeOffset SessionStart(DateTime date, TimeSpan time);
}

public sealed class GalleryClock(TimeProvider timeProvider, IOptions<GalleryOptions> options) : IGalleryClock
{
    private readonly TimeZoneInfo _timeZone = Resolve(options.Value.TimeZoneId);
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    public DateTimeOffset GalleryNow => TimeZoneInfo.ConvertTime(UtcNow, _timeZone);

    public DateTimeOffset SessionStart(DateTime date, TimeSpan time)
    {
        var local = DateTime.SpecifyKind(date.Date.Add(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, _timeZone.GetUtcOffset(local));
    }

    private static TimeZoneInfo Resolve(string configured)
    {
        foreach (var id in new[] { configured, "America/Sao_Paulo", "E. South America Standard Time" }.Distinct())
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        throw new InvalidOperationException("O fuso horário da Galeria Aurora não pôde ser carregado.");
    }
}

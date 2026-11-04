namespace AuroraGaleria.Models;

public class HomeViewModel
{
    public IReadOnlyList<Exposicao> Exposicoes { get; init; } = [];
    public IReadOnlyList<Produto> Produtos { get; init; } = [];
    public IReadOnlyList<SessionOption> Sessoes { get; init; } = [];
}

public record SessionOption(int Id, int ExposureId, string ExposureName, string Date, string Time, int Available);

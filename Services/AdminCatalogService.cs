using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Services;

public interface IAdminCatalogService
{
    Task<AdminDashboardViewModel> GetDashboardAsync();
    Task<ServiceResult<int>> CreateExposureAsync(ExposureAdminRequest request);
    Task<ServiceResult<int>> UpdateExposureAsync(int id, ExposureAdminRequest request);
    Task<ServiceResult<int>> ToggleExposureAsync(int id);
    Task<ServiceResult<int>> CreateProductAsync(ProductAdminRequest request);
    Task<ServiceResult<int>> UpdateProductAsync(int id, ProductAdminRequest request);
    Task<ServiceResult<int>> ToggleProductAsync(int id);
    Task<ServiceResult<int>> CreateSessionAsync(SessionAdminRequest request);
    Task<ServiceResult<int>> ToggleSessionAsync(int id);
}

public class AdminCatalogService(AppDbContext db, TimeProvider timeProvider) : IAdminCatalogService
{
    public async Task<AdminDashboardViewModel> GetDashboardAsync()
    {
        var sessions = await db.VisitSessions.AsNoTracking().Include(item => item.Exposure)
            .OrderBy(item => item.VisitDate).ToListAsync();
        return new AdminDashboardViewModel
        {
            Exposures = await db.Exposicoes.AsNoTracking().OrderBy(item => item.Inicio).ToListAsync(),
            Products = await db.Produtos.AsNoTracking().OrderBy(item => item.Nome).ToListAsync(),
            Sessions = sessions.OrderBy(item => item.VisitDate).ThenBy(item => item.StartTime).ToList()
        };
    }

    public async Task<ServiceResult<int>> CreateExposureAsync(ExposureAdminRequest request)
    {
        var validation = ValidateExposure(request);
        if (validation is not null) return ServiceResult<int>.Fail("invalid", validation);
        var exposure = new Exposicao
        {
            Titulo = request.Title.Trim(), Descricao = request.Description.Trim(), ImagemUrl = request.ImageUrl.Trim(),
            Periodo = request.Period.Trim(), Inicio = request.StartDate.Date, Fim = request.EndDate.Date, Ativa = true
        };
        db.Exposicoes.Add(exposure);
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(exposure.Id);
    }

    public async Task<ServiceResult<int>> UpdateExposureAsync(int id, ExposureAdminRequest request)
    {
        var validation = ValidateExposure(request);
        if (validation is not null) return ServiceResult<int>.Fail("invalid", validation);
        var exposure = await db.Exposicoes.SingleOrDefaultAsync(item => item.Id == id);
        if (exposure is null) return ServiceResult<int>.Fail("not_found", "Exposição não encontrada.");
        if (await db.VisitSessions.AnyAsync(item => item.ExposureId == id && (item.VisitDate < request.StartDate.Date || item.VisitDate > request.EndDate.Date)))
            return ServiceResult<int>.Fail("conflict", "O novo período deixaria sessões cadastradas fora da exposição. Ajuste ou desative essas sessões primeiro.");

        exposure.Titulo = request.Title.Trim(); exposure.Descricao = request.Description.Trim(); exposure.ImagemUrl = request.ImageUrl.Trim();
        exposure.Periodo = request.Period.Trim(); exposure.Inicio = request.StartDate.Date; exposure.Fim = request.EndDate.Date;
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(exposure.Id);
    }

    public async Task<ServiceResult<int>> ToggleExposureAsync(int id)
    {
        var exposure = await db.Exposicoes.SingleOrDefaultAsync(item => item.Id == id);
        if (exposure is null) return ServiceResult<int>.Fail("not_found", "Exposição não encontrada.");
        exposure.Ativa = !exposure.Ativa;
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(id);
    }

    public async Task<ServiceResult<int>> CreateProductAsync(ProductAdminRequest request)
    {
        var validation = ValidateProduct(request);
        if (validation is not null) return ServiceResult<int>.Fail("invalid", validation);
        var product = new Produto { Nome = request.Name.Trim(), Preco = request.Price, ImagemUrl = request.ImageUrl.Trim(), Ativo = true };
        db.Produtos.Add(product);
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(product.Id);
    }

    public async Task<ServiceResult<int>> UpdateProductAsync(int id, ProductAdminRequest request)
    {
        var validation = ValidateProduct(request);
        if (validation is not null) return ServiceResult<int>.Fail("invalid", validation);
        var product = await db.Produtos.SingleOrDefaultAsync(item => item.Id == id);
        if (product is null) return ServiceResult<int>.Fail("not_found", "Produto não encontrado.");
        product.Nome = request.Name.Trim(); product.Preco = request.Price; product.ImagemUrl = request.ImageUrl.Trim();
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(id);
    }

    public async Task<ServiceResult<int>> ToggleProductAsync(int id)
    {
        var product = await db.Produtos.SingleOrDefaultAsync(item => item.Id == id);
        if (product is null) return ServiceResult<int>.Fail("not_found", "Produto não encontrado.");
        product.Ativo = !product.Ativo;
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(id);
    }

    public async Task<ServiceResult<int>> CreateSessionAsync(SessionAdminRequest request)
    {
        var exposure = await db.Exposicoes.SingleOrDefaultAsync(item => item.Id == request.ExposureId);
        var today = timeProvider.GetLocalNow().Date;
        if (request.Capacity < 1 || exposure is null || !exposure.Ativa || request.VisitDate.Date < today ||
            request.VisitDate.Date < exposure.Inicio.Date || request.VisitDate.Date > exposure.Fim.Date)
            return ServiceResult<int>.Fail("invalid", "A sessão deve ter capacidade positiva, pertencer a uma exposição ativa, estar no período dela e não estar no passado.");
        if (await db.VisitSessions.AnyAsync(item => item.ExposureId == request.ExposureId && item.VisitDate == request.VisitDate.Date && item.StartTime == request.StartTime))
            return ServiceResult<int>.Fail("conflict", "Já existe uma sessão neste dia e horário.");

        var session = new VisitSession { ExposureId = request.ExposureId, VisitDate = request.VisitDate.Date, StartTime = request.StartTime, Capacity = request.Capacity, IsActive = true };
        db.VisitSessions.Add(session);
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(session.Id);
    }

    public async Task<ServiceResult<int>> ToggleSessionAsync(int id)
    {
        var session = await db.VisitSessions.SingleOrDefaultAsync(item => item.Id == id);
        if (session is null) return ServiceResult<int>.Fail("not_found", "Sessão não encontrada.");
        session.IsActive = !session.IsActive;
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(id);
    }

    private static string? ValidateExposure(ExposureAdminRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.ImageUrl) || string.IsNullOrWhiteSpace(request.Period))
            return "Título, imagem e período são obrigatórios.";
        return request.StartDate == default || request.EndDate.Date < request.StartDate.Date ? "O período da exposição é inválido." : null;
    }

    private static string? ValidateProduct(ProductAdminRequest request)
    {
        return string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ImageUrl) || request.Price <= 0
            ? "Nome, imagem e preço positivo são obrigatórios." : null;
    }
}

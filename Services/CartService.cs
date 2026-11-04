using System.Data;
using AuroraGaleria.Data;
using AuroraGaleria.Models;
using Microsoft.EntityFrameworkCore;

namespace AuroraGaleria.Services;

public interface ICartService
{
    Task<CartDto> GetAsync(int userId);
    Task<ServiceResult<CartDto>> AddAsync(int userId, AddCartItemRequest request);
    Task<ServiceResult<CartDto>> RemoveAsync(int userId, int itemId);
    Task<ServiceResult<CheckoutDto>> CheckoutAsync(int userId, CheckoutRequest request);
}

public record CartLineDto(int Id, int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal);
public record CartDto(IReadOnlyCollection<CartLineDto> Items, int Count, decimal Total);
public record CheckoutDto(CartDto Cart, int? OrderId, bool PriceChanged, bool AlreadyProcessed);

public class CartService(AppDbContext db) : ICartService
{
    public async Task<CartDto> GetAsync(int userId)
    {
        var items = await db.CartItems.AsNoTracking().Where(item => item.UserId == userId).OrderBy(item => item.CreatedAt).ToListAsync();
        return Map(items);
    }

    public async Task<ServiceResult<CartDto>> AddAsync(int userId, AddCartItemRequest request)
    {
        if (request.Quantity < 1 || request.Quantity > 20)
        {
            return ServiceResult<CartDto>.Fail("invalid_quantity", "Informe uma quantidade entre 1 e 20.");
        }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await db.IdempotencyRecords.AnyAsync(item => item.UserId == userId && item.Scope == "cart-add" && item.RequestId == request.RequestId))
        {
            return ServiceResult<CartDto>.Ok(await GetAsync(userId), "Solicitação já processada.");
        }

        var product = await db.Produtos.SingleOrDefaultAsync(item => item.Id == request.ProductId);
        if (product is null)
        {
            return ServiceResult<CartDto>.Fail("not_found", "Produto não encontrado.");
        }
        if (!product.Ativo)
        {
            return ServiceResult<CartDto>.Fail("inactive", "Este produto não está disponível.");
        }

        var item = await db.CartItems.SingleOrDefaultAsync(cart => cart.UserId == userId && cart.ProductId == product.Id);
        if (item is null)
        {
            db.CartItems.Add(new CartItem
            {
                UserId = userId, ProductId = product.Id, ProductName = product.Nome,
                UnitPrice = product.Preco, Quantity = request.Quantity, CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            if (item.Quantity + request.Quantity > 20)
            {
                return ServiceResult<CartDto>.Fail("invalid_quantity", "A quantidade máxima por produto é 20.");
            }
            item.Quantity += request.Quantity;
            item.ProductName = product.Nome;
            item.UnitPrice = product.Preco;
        }

        db.IdempotencyRecords.Add(new IdempotencyRecord { UserId = userId, Scope = "cart-add", RequestId = request.RequestId });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult<CartDto>.Ok(await GetAsync(userId));
    }

    public async Task<ServiceResult<CartDto>> RemoveAsync(int userId, int itemId)
    {
        var item = await db.CartItems.SingleOrDefaultAsync(cart => cart.Id == itemId && cart.UserId == userId);
        if (item is null)
        {
            return ServiceResult<CartDto>.Fail("not_found", "Item não encontrado no carrinho.");
        }

        db.CartItems.Remove(item);
        await db.SaveChangesAsync();
        return ServiceResult<CartDto>.Ok(await GetAsync(userId));
    }

    public async Task<ServiceResult<CheckoutDto>> CheckoutAsync(int userId, CheckoutRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var previous = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(order => order.UserId == userId && order.RequestId == request.RequestId);
        if (previous is not null)
        {
            return ServiceResult<CheckoutDto>.Ok(new CheckoutDto(await GetAsync(userId), previous.Id, false, true), "Solicitação já processada.");
        }

        var items = await db.CartItems.Where(item => item.UserId == userId).OrderBy(item => item.Id).ToListAsync();
        if (items.Count == 0)
        {
            return ServiceResult<CheckoutDto>.Fail("empty", "Seu carrinho está vazio.");
        }

        var productIds = items.Select(item => item.ProductId).ToArray();
        var products = await db.Produtos.Where(item => productIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id);
        if (items.Any(item => !products.TryGetValue(item.ProductId, out var product) || !product.Ativo))
        {
            return ServiceResult<CheckoutDto>.Fail("unavailable", "Um ou mais produtos do carrinho não estão mais disponíveis.");
        }

        var changed = false;
        foreach (var item in items)
        {
            var product = products[item.ProductId];
            if (item.UnitPrice != product.Preco || item.ProductName != product.Nome)
            {
                item.UnitPrice = product.Preco;
                item.ProductName = product.Nome;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return ServiceResult<CheckoutDto>.Fail("price_changed", "O preço de um produto mudou. Revise o carrinho e confirme novamente.", new CheckoutDto(Map(items), null, true, false));
        }

        var order = new PurchaseOrder
        {
            UserId = userId, RequestId = request.RequestId, Status = "Simulated",
            TotalPrice = items.Sum(item => item.UnitPrice * item.Quantity), CreatedAt = DateTime.UtcNow,
            Items = items.Select(item => new PurchaseItem
            {
                ProductId = item.ProductId, ProductName = item.ProductName, UnitPrice = item.UnitPrice,
                Quantity = item.Quantity, TotalPrice = item.UnitPrice * item.Quantity
            }).ToList()
        };
        db.PurchaseOrders.Add(order);
        db.CartItems.RemoveRange(items);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult<CheckoutDto>.Ok(new CheckoutDto(new CartDto([], 0, 0), order.Id, false, false), "Simulação de compra confirmada.");
    }

    private static CartDto Map(IReadOnlyCollection<CartItem> items) => new(
        items.Select(item => new CartLineDto(item.Id, item.ProductId, item.ProductName, item.UnitPrice, item.Quantity, item.UnitPrice * item.Quantity)).ToList(),
        items.Sum(item => item.Quantity), items.Sum(item => item.UnitPrice * item.Quantity));
}

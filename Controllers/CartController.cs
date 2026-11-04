using System.Security.Claims;
using AuroraGaleria.Models;
using AuroraGaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuroraGaleria.Controllers;

[ApiController, Authorize, Route("cart")]
public class CartController(ICartService cartService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await cartService.GetAsync(UserId));

    [HttpPost("items")]
    public async Task<IActionResult> Add(AddCartItemRequest request)
    {
        var result = await cartService.AddAsync(UserId, request);
        return result.Success ? Ok(result.Value) : Problem(result);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(CheckoutRequest request)
    {
        var result = await cartService.CheckoutAsync(UserId, request);
        if (result.Success)
            return Ok(new { result.Value!.Cart, result.Value.OrderId, result.Value.AlreadyProcessed, message = result.Message });
        if (result.Code == "price_changed")
            return Conflict(new { message = result.Message, priceChanged = true, cart = result.Value?.Cart });
        return Problem(result);
    }

    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> Remove(int id)
    {
        var result = await cartService.RemoveAsync(UserId, id);
        return result.Success ? Ok(result.Value) : Problem(result);
    }

    private IActionResult Problem<T>(ServiceResult<T> result) => result.Code switch
    {
        "not_found" => NotFound(new { message = result.Message }),
        "inactive" or "unavailable" or "empty" or "invalid_quantity" => BadRequest(new { message = result.Message }),
        _ => Conflict(new { message = result.Message })
    };

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

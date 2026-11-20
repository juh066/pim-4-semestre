using AuroraGaleria.Models;
using AuroraGaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AuroraGaleria.Controllers;

[Authorize(Roles = UserRoles.Administrator), Route("admin")]
public class AdminController(IAdminCatalogService catalog, IPrivacyService privacy) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await catalog.GetDashboardAsync());

    [HttpPost("exposures")]
    public async Task<IActionResult> CreateExposure(ExposureAdminRequest request) =>
        await Handle(await catalog.CreateExposureAsync(request));

    [HttpPost("exposures/{id:int}/edit")]
    public async Task<IActionResult> EditExposure(int id, ExposureAdminRequest request) =>
        await Handle(await catalog.UpdateExposureAsync(id, request));

    [HttpPost("exposures/{id:int}/toggle")]
    public async Task<IActionResult> ToggleExposure(int id) =>
        await Handle(await catalog.ToggleExposureAsync(id));

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(ProductAdminRequest request) =>
        await Handle(await catalog.CreateProductAsync(request));

    [HttpPost("products/{id:int}/edit")]
    public async Task<IActionResult> EditProduct(int id, ProductAdminRequest request) =>
        await Handle(await catalog.UpdateProductAsync(id, request));

    [HttpPost("products/{id:int}/toggle")]
    public async Task<IActionResult> ToggleProduct(int id) =>
        await Handle(await catalog.ToggleProductAsync(id));

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(SessionAdminRequest request) =>
        await Handle(await catalog.CreateSessionAsync(UserId, request));

    [HttpPost("sessions/{id:int}/edit")]
    public async Task<IActionResult> EditSession(int id, UpdateSessionAdminRequest request) =>
        await Handle(await catalog.UpdateSessionAsync(UserId, id, request));

    [HttpPost("sessions/{id:int}/toggle")]
    public async Task<IActionResult> ToggleSession(int id) =>
        await Handle(await catalog.ToggleSessionAsync(UserId, id));

    [HttpPost("sessions/{id:int}/cancel")]
    public async Task<IActionResult> CancelSession(int id, CancelSessionAdminRequest request) =>
        await Handle(await catalog.CancelSessionAsync(UserId, id, request.Reason));

    [HttpPost("deletion-requests/{id:int}/complete")]
    public async Task<IActionResult> CompleteDeletion(int id)
    {
        var result = await privacy.CompleteDeletionAsync(id, UserId);
        if (!result.Success) TempData["AdminError"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private Task<IActionResult> Handle(ServiceResult<int> result)
    {
        if (!result.Success) TempData["AdminError"] = result.Message;
        return Task.FromResult<IActionResult>(result.Code == "not_found" ? NotFound() : RedirectToAction(nameof(Index)));
    }
}

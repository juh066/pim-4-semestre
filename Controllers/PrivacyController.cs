using System.Security.Claims;
using AuroraGaleria.Models;
using AuroraGaleria.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuroraGaleria.Controllers;

[ApiController, Authorize, Route("privacy")]
public sealed class PrivacyController(IPrivacyService privacy) : ControllerBase
{
    [HttpPost("deletion-requests")]
    public async Task<IActionResult> RequestDeletion()
    {
        var result = await privacy.RequestDeletionAsync(UserId);
        return result.Success ? Accepted(new { id = result.Value, message = result.Message }) : NotFound(new { message = result.Message });
    }

    [Authorize(Roles = UserRoles.Administrator), HttpGet("deletion-requests")]
    public async Task<IActionResult> Pending()
    {
        var items = await privacy.ListPendingAsync();
        return Ok(items.Select(item => new { item.Id, item.UserId, name = item.User?.Name, email = item.User?.Email, item.RequestedAtUtc }));
    }

    [Authorize(Roles = UserRoles.Administrator), HttpPost("deletion-requests/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var result = await privacy.CompleteDeletionAsync(id, UserId);
        return result.Success ? Ok(new { message = result.Message }) : NotFound(new { message = result.Message });
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

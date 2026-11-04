using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuroraGaleria.Controllers
{
    [Authorize(Roles = AuroraGaleria.Models.UserRoles.Administrator)]
    [Route("analytics")]
    public class AnalyticsController : Controller
    {
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }
    }
}

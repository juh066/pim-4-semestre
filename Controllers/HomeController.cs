using Microsoft.AspNetCore.Mvc;
using AuroraGaleria.Models;
using AuroraGaleria.Data;
using Microsoft.EntityFrameworkCore;
using AuroraGaleria.Services;

namespace AuroraGaleria.Controllers
{
    public class HomeController(AppDbContext db, IGalleryClock clock) : Controller
    {
        [HttpGet("/sessions/{id:int}/availability")]
        public async Task<IActionResult> SessionAvailability(int id)
        {
            var today = clock.GalleryNow.Date;
            var session = await db.VisitSessions.AsNoTracking()
                .Include(item => item.Exposure)
                .SingleOrDefaultAsync(item => item.Id == id);

            var isAvailable = session is not null &&
                session.IsActive &&
                session.Status == SessionStatuses.Open &&
                session.Exposure is not null &&
                session.Exposure.Ativa &&
                session.VisitDate >= today &&
                session.VisitDate >= session.Exposure.Inicio &&
                session.VisitDate <= session.Exposure.Fim &&
                session.ReservedQuantity < session.Capacity;
            isAvailable = isAvailable && clock.SessionStart(session!.VisitDate, session.StartTime) > clock.GalleryNow;

            return Ok(new
            {
                sessionId = id,
                isAvailable,
                available = isAvailable ? session!.Capacity - session.ReservedQuantity : 0
            });
        }

        public async Task<IActionResult> Index()
        {
            var today = clock.GalleryNow.Date;
            var sessions = await db.VisitSessions.AsNoTracking().Include(item => item.Exposure)
                .Where(item => item.IsActive && item.Status == SessionStatuses.Open && item.Exposure!.Ativa && item.VisitDate >= today && item.VisitDate >= item.Exposure.Inicio && item.VisitDate <= item.Exposure.Fim)
                .OrderBy(item => item.VisitDate).ToListAsync();
            sessions = sessions.Where(item => clock.SessionStart(item.VisitDate, item.StartTime) > clock.GalleryNow).ToList();
            sessions = sessions.OrderBy(item => item.VisitDate).ThenBy(item => item.StartTime).ToList();
            var model = new HomeViewModel
            {
                Exposicoes = await db.Exposicoes.AsNoTracking().Where(item => item.Ativa).OrderBy(item => item.Inicio).ToListAsync(),
                Produtos = await db.Produtos.AsNoTracking().Where(item => item.Ativo).OrderBy(item => item.Nome).ToListAsync(),
                Sessoes = sessions.Select(item => new SessionOption(item.Id, item.ExposureId, item.Exposure!.Titulo,
                    item.VisitDate.ToString("dd/MM/yyyy"), item.StartTime.ToString(@"hh\:mm"), item.Capacity - item.ReservedQuantity)).ToList()
            };
            return View(model);
        }

        [HttpGet("/privacidade")]
        public IActionResult Privacy() => View();
    }
}

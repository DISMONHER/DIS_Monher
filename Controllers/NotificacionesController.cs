using DIS_Monher.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DIS_Monher.Controllers
{
    public class NotificacionesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificacionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var notificaciones = await _context.Notificaciones
                .Include(n => n.OrdenProduccion)
                .Where(n => n.Area == "Distribución")
                .OrderByDescending(n => n.Fecha)
                .ToListAsync();

            return View(notificaciones);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarLeida(int id)
        {
            var notificacion = await _context.Notificaciones
                .FindAsync(id);

            if (notificacion == null)
                return NotFound();

            notificacion.Leida = true;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
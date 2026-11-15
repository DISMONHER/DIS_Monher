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

        public async Task<IActionResult> Index(string? area)
        {
            var query = _context.Notificaciones
                .Include(n => n.OrdenProduccion)
                .Include(n => n.Pedido)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(area))
            {
                query = query.Where(n => n.Area == area);
            }

            var notificaciones = await query
                .OrderByDescending(n => n.Fecha)
                .ToListAsync();

            ViewBag.Area = area;

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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DIS_Monher.Data;
using DIS_Monher.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DIS_Monher.Controllers
{
    public class FinishedProductsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public FinishedProductsController(ApplicationDbContext context) => _context = context;

        // HU004: Consultar y Registrar Productos Terminados
        public async Task<IActionResult> Index()
        {
            ViewBag.Materiales = await _context.Materiales.ToListAsync();
            return View(await _context.ProductosTerminados.Include(p => p.Composicion).ThenInclude(c => c.Material).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductoTerminado producto, int[]? materialIds, decimal[]? cantidades)
        {
            if (await _context.ProductosTerminados.AnyAsync(p => p.Codigo == producto.Codigo))
            {
                TempData["Error"] = $"El código {producto.Codigo} ya existe.";
                return RedirectToAction(nameof(Index));
            }

            producto.StockActual = 0;
            producto.Composicion = new();
            materialIds ??= Array.Empty<int>();
            cantidades ??= Array.Empty<decimal>();

            for (int i = 0; i < materialIds.Length && i < cantidades.Length; i++)
            {
                if (cantidades[i] <= 0 || producto.Composicion.Any(c => c.MaterialId == materialIds[i])) continue;
                producto.Composicion.Add(new ProductoMaterial { MaterialId = materialIds[i], CantidadRequerida = cantidades[i] });
            }

            _context.ProductosTerminados.Add(producto);
            await _context.SaveChangesAsync();
            TempData["Ok"] = "Producto registrado correctamente.";
            return RedirectToAction(nameof(Index));
        }
    }
}
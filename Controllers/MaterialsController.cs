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
    public class MaterialsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public MaterialsController(ApplicationDbContext context) => _context = context;

        // HU001: Listar y Registrar Materiales
        public async Task<IActionResult> Index()
        {
            return View(await _context.Materiales.ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Create(Material material)
        {
            if (!ModelState.IsValid || material.StockMinimo < 0)
            {
                TempData["Error"] = "Complete correctamente los campos del material.";
                return RedirectToAction(nameof(Index));
            }
            if (await _context.Materiales.AnyAsync(m => m.Codigo == material.Codigo))
            {
                TempData["Error"] = $"El código {material.Codigo} ya existe.";
                return RedirectToAction(nameof(Index));
            }

            material.FechaIngreso = DateTime.Now;
            material.StockActual = 0;
            _context.Materiales.Add(material);
            await _context.SaveChangesAsync();
            InventarioHelper.EvaluarAlerta(_context, material); // nace en 0 → alerta si mínimo > 0
            await _context.SaveChangesAsync();

            TempData["Ok"] = "Material registrado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> RegistrarMovimiento(int materialId, string tipo, decimal cantidad, string motivo, string responsable)
        {
            if (cantidad <= 0)
            {
                TempData["Error"] = "La cantidad debe ser mayor a cero.";
                return RedirectToAction(nameof(ControlInventario));
            }

            var material = await _context.Materiales.FindAsync(materialId);
            if (material == null) return NotFound();

            if (tipo == "SALIDA" && material.StockActual < cantidad)
            {
                TempData["Error"] = $"Stock insuficiente: disponible {material.StockActual} {material.UnidadMedida}.";
                return RedirectToAction(nameof(ControlInventario));
            }

            material.StockActual += tipo == "ENTRADA" ? cantidad : -cantidad;

            _context.MovimientosInventario.Add(new MovimientoInventario
            {
                MaterialId = materialId, TipoMovimiento = tipo, Cantidad = cantidad,
                Motivo = motivo, Responsable = responsable, Fecha = DateTime.Now
            });

            InventarioHelper.EvaluarAlerta(_context, material); // sin duplicados, y se cierra sola al reponer
            await _context.SaveChangesAsync();

            TempData["Ok"] = "Movimiento registrado correctamente.";
            return RedirectToAction(nameof(ControlInventario));
        }

        // HU003: Consultar Stock Disponible
        public async Task<IActionResult> ControlInventario()
        {
            ViewBag.Materiales = await _context.Materiales.ToListAsync();
            ViewBag.Alertas = await _context.AlertasStock.Include(a => a.Material).Where(a => !a.Atendida).ToListAsync();
            return View(await _context.MovimientosInventario.Include(m => m.Material).OrderByDescending(m => m.Fecha).ToListAsync());
        }
    }
}
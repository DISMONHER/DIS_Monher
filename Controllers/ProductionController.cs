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
    public class ProductionController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ProductionController(ApplicationDbContext context) => _context = context;

        // HU006 & HU010: Listar y crear órdenes / Historial
        public async Task<IActionResult> Index(string? estado, DateTime? desde, DateTime? hasta)
{
    var query = _context.OrdenesProduccion.Include(o => o.MaterialesAsignados).ThenInclude(m => m.Material).AsQueryable();
    if (!string.IsNullOrEmpty(estado)) query = query.Where(o => o.Estado == estado);
    if (desde.HasValue) query = query.Where(o => o.FechaCreacion >= desde.Value);
    if (hasta.HasValue) query = query.Where(o => o.FechaCreacion < hasta.Value.Date.AddDays(1)); // incluye todo el día

    ViewBag.Productos = await _context.ProductosTerminados.ToListAsync();
    return View(await query.OrderByDescending(o => o.FechaCreacion).ToListAsync());
}

[HttpPost]
public async Task<IActionResult> Create(OrdenProduccion orden)
{
    var producto = await _context.ProductosTerminados
        .Include(p => p.Composicion).ThenInclude(c => c.Material)
        .FirstOrDefaultAsync(p => p.Nombre == orden.TipoCaja);

    if (producto == null || orden.Cantidad <= 0)
    {
        TempData["Error"] = "Seleccione un producto válido y una cantidad mayor a cero.";
        return RedirectToAction(nameof(Index));
    }

    // Verificar materiales según la composición (receta) del producto
    var faltantes = producto.Composicion
        .Where(c => c.Material!.StockActual < c.CantidadRequerida * orden.Cantidad)
        .Select(c => c.Material!.Nombre).ToList();
    if (faltantes.Any())
    {
        TempData["Error"] = "Materiales insuficientes para producir: " + string.Join(", ", faltantes);
        return RedirectToAction(nameof(Index));
    }

    orden.NumeroOrden = "TEMP";
    orden.Estado = "Pendiente";
    orden.FechaCreacion = DateTime.Now;
    _context.OrdenesProduccion.Add(orden);
    await _context.SaveChangesAsync();
    orden.NumeroOrden = $"OP-{orden.Id:D5}";

    // HU007: asignación y descuento automático de materiales + trazabilidad en movimientos
    foreach (var c in producto.Composicion)
    {
        var requerido = c.CantidadRequerida * orden.Cantidad;
        c.Material!.StockActual -= requerido;

        _context.DetalleOrdenMateriales.Add(new DetalleOrdenMaterial
        { OrdenProduccionId = orden.Id, MaterialId = c.MaterialId, Cantidad = requerido });

        _context.MovimientosInventario.Add(new MovimientoInventario
        {
            MaterialId = c.MaterialId, TipoMovimiento = "SALIDA", Cantidad = requerido,
            Motivo = $"Producción {orden.NumeroOrden}", Responsable = orden.Responsable
        });

        InventarioHelper.EvaluarAlerta(_context, c.Material);
    }
    await _context.SaveChangesAsync();

    TempData["Ok"] = $"Orden {orden.NumeroOrden} creada y materiales descontados del inventario.";
    return RedirectToAction(nameof(Index));
}

// HU007 manual (extras). Corrige: duplicados de clave compuesta, movimientos y alertas
[HttpPost]
public async Task<IActionResult> AsignarMateriales(int ordenId, int[] materialIds, decimal[] cantidades)
{
    for (int i = 0; i < materialIds.Length; i++)
    {
        var mat = await _context.Materiales.FindAsync(materialIds[i]);
        if (mat == null || cantidades[i] <= 0 || mat.StockActual < cantidades[i]) continue;

        mat.StockActual -= cantidades[i];
        var detalle = await _context.DetalleOrdenMateriales.FindAsync(ordenId, materialIds[i]);
        if (detalle == null)
            _context.DetalleOrdenMateriales.Add(new DetalleOrdenMaterial { OrdenProduccionId = ordenId, MaterialId = materialIds[i], Cantidad = cantidades[i] });
        else detalle.Cantidad += cantidades[i];

        _context.MovimientosInventario.Add(new MovimientoInventario
        { MaterialId = mat.Id, TipoMovimiento = "SALIDA", Cantidad = cantidades[i], Motivo = $"Material adicional orden #{ordenId}", Responsable = "Producción" });
        InventarioHelper.EvaluarAlerta(_context, mat);
    }
    await _context.SaveChangesAsync();
    return RedirectToAction(nameof(Index));
}

// HU008: el avance alimenta el stock de producto terminado
[HttpPost]
public async Task<IActionResult> ActualizarAvance(int ordenId, string estado, int cantidadProducida)
{
    var orden = await _context.OrdenesProduccion.Include(o => o.MaterialesAsignados).FirstOrDefaultAsync(o => o.Id == ordenId);
    if (orden == null) return NotFound();

    if (orden.Estado is "Terminado" or "Cancelado")
    {
        TempData["Error"] = "La orden ya está cerrada y no puede modificarse.";
        return RedirectToAction(nameof(Index));
    }
    if (cantidadProducida < orden.CantidadProducida || cantidadProducida > orden.Cantidad)
    {
        TempData["Error"] = $"La cantidad producida debe estar entre {orden.CantidadProducida} y {orden.Cantidad}.";
        return RedirectToAction(nameof(Index));
    }

    if (estado == "Cancelado")
    {
        // Devolver al inventario la proporción de material no utilizada
        var pendiente = orden.Cantidad - orden.CantidadProducida;
        foreach (var d in orden.MaterialesAsignados)
        {
            var devolver = Math.Round(d.Cantidad * pendiente / orden.Cantidad, 2);
            var mat = await _context.Materiales.FindAsync(d.MaterialId);
            if (mat == null || devolver <= 0) continue;
            mat.StockActual += devolver;
            _context.MovimientosInventario.Add(new MovimientoInventario
            { MaterialId = mat.Id, TipoMovimiento = "ENTRADA", Cantidad = devolver, Motivo = $"Devolución por cancelar {orden.NumeroOrden}", Responsable = orden.Responsable });
            InventarioHelper.EvaluarAlerta(_context, mat);
        }
        orden.Estado = "Cancelado";
        orden.FechaFin = DateTime.Now;
    }
    else
    {
        var nuevas = cantidadProducida - orden.CantidadProducida;
        var producto = await _context.ProductosTerminados.FirstOrDefaultAsync(p => p.Nombre == orden.TipoCaja);
        if (producto != null && nuevas > 0) producto.StockActual += nuevas; // ← entra al stock vendible

        orden.CantidadProducida = cantidadProducida;
        orden.Estado = cantidadProducida >= orden.Cantidad ? "Terminado" : "En Proceso";
        if (orden.Estado == "Terminado") orden.FechaFin = DateTime.Now;
    }

    await _context.SaveChangesAsync();
    TempData["Ok"] = $"Orden {orden.NumeroOrden} actualizada: {orden.Estado}.";
    return RedirectToAction(nameof(Index));
}

        // HU009: Control de Calidad
        [HttpPost]
        public async Task<IActionResult> RegistrarCalidad(InspeccionCalidad inspeccion)
        {
            _context.InspeccionesCalidad.Add(inspeccion);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
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
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace DIS_Monher.Controllers
{
    public class ProductionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProductionController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // HU006 & HU010: Listar y crear órdenes / Historial
        public async Task<IActionResult> Index(string? estado, DateTime? desde, DateTime? hasta)
{
    var query = _context.OrdenesProduccion.Include(o => o.MaterialesAsignados).ThenInclude(m => m.Material).Include(o => o.Avances).AsQueryable();
    if (!string.IsNullOrEmpty(estado)) query = query.Where(o => o.Estado == estado);
    if (desde.HasValue) query = query.Where(o => o.FechaCreacion >= desde.Value);
    if (hasta.HasValue) query = query.Where(o => o.FechaCreacion < hasta.Value.Date.AddDays(1)); // incluye todo el día

    ViewBag.Productos = await _context.ProductosTerminados.ToListAsync();
    ViewBag.Materiales = await _context.Materiales
    .OrderBy(m => m.Nombre)
    .ToListAsync();
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
        { OrdenProduccionId = orden.Id, MaterialId = c.MaterialId, Cantidad = requerido, ResponsableAsignacion = orden.Responsable, FechaAsignacion = DateTime.Now });

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
public async Task<IActionResult> AsignarMateriales(
    int ordenId,
    int[] materialIds,
    decimal[] cantidades,
    string responsable,
    string? justificacion)
{
    // 1. Validar orden
    var orden = await _context.OrdenesProduccion
        .Include(o => o.MaterialesAsignados)
        .FirstOrDefaultAsync(o => o.Id == ordenId);

    if (orden == null)
        return NotFound();

    if (orden.Estado is "Terminado" or "Cancelado")
    {
        TempData["Error"] = "No se pueden asignar materiales a una orden cerrada.";
        return RedirectToAction(nameof(Index));
    }

    // 2. Validar responsable
    if (string.IsNullOrWhiteSpace(responsable))
    {
        TempData["Error"] = "Debe indicar el responsable de la asignación.";
        return RedirectToAction(nameof(Index));
    }

    // 3. Validar datos recibidos
    if (materialIds == null ||
        cantidades == null ||
        materialIds.Length == 0 ||
        materialIds.Length != cantidades.Length)
    {
        TempData["Error"] = "Debe seleccionar un material y una cantidad válida.";
        return RedirectToAction(nameof(Index));
    }

    // 4. Validar TODO antes de descontar stock
    for (int i = 0; i < materialIds.Length; i++)
    {
        if (cantidades[i] <= 0)
        {
            TempData["Error"] = "La cantidad debe ser mayor a cero.";
            return RedirectToAction(nameof(Index));
        }

        var material = await _context.Materiales.FindAsync(materialIds[i]);

        if (material == null)
        {
            TempData["Error"] = "Uno de los materiales seleccionados no existe.";
            return RedirectToAction(nameof(Index));
        }

        if (material.StockActual < cantidades[i])
        {
            TempData["Error"] =
                $"Stock insuficiente para {material.Nombre}. Disponible: {material.StockActual}.";
            return RedirectToAction(nameof(Index));
        }

        var detalleExistente = orden.MaterialesAsignados
            .FirstOrDefault(d => d.MaterialId == materialIds[i]);

        if (detalleExistente != null &&
            string.IsNullOrWhiteSpace(justificacion))
        {
            TempData["Error"] =
                $"El material {material.Nombre} ya está asignado. Debe indicar una justificación para realizar el ajuste.";

            return RedirectToAction(nameof(Index));
        }
    }

    for (int i = 0; i < materialIds.Length; i++)
    {
        var material = await _context.Materiales.FindAsync(materialIds[i]);

        if (material == null)
            continue;

        var detalle = orden.MaterialesAsignados
            .FirstOrDefault(d => d.MaterialId == materialIds[i]);

        material.StockActual -= cantidades[i];

        if (detalle == null)
        {
            detalle = new DetalleOrdenMaterial
            {
                OrdenProduccionId = ordenId,
                MaterialId = materialIds[i],
                Cantidad = cantidades[i],
                ResponsableAsignacion = responsable,
                FechaAsignacion = DateTime.Now
            };

            _context.DetalleOrdenMateriales.Add(detalle);
        }
        else
        {
            detalle.Cantidad += cantidades[i];
            detalle.ResponsableAsignacion = responsable;
            detalle.JustificacionAjuste = justificacion;
            detalle.FechaUltimoAjuste = DateTime.Now;
        }

        _context.MovimientosInventario.Add(new MovimientoInventario
        {
            MaterialId = material.Id,
            TipoMovimiento = "SALIDA",
            Cantidad = cantidades[i],
            Motivo = detalle.FechaUltimoAjuste.HasValue
                ? $"Ajuste de material en {orden.NumeroOrden}: {justificacion}"
                : $"Asignación de material a {orden.NumeroOrden}",
            Responsable = responsable
        });

        InventarioHelper.EvaluarAlerta(_context, material);
    }

    await _context.SaveChangesAsync();

    TempData["Ok"] =
        $"Materiales asignados correctamente a la orden {orden.NumeroOrden}.";

    return RedirectToAction(nameof(Index));
}

[HttpPost]
public async Task<IActionResult> ActualizarAvance(int ordenId, string estado, int cantidadProducida, string observaciones)
{
    var orden = await _context.OrdenesProduccion.Include(o => o.MaterialesAsignados).FirstOrDefaultAsync(o => o.Id == ordenId);
    if (orden == null) return NotFound();
    var estadoAnterior = orden.Estado;

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
        orden.CantidadProducida = cantidadProducida;
        orden.Estado = cantidadProducida >= orden.Cantidad ? "Terminado" : "En Proceso";
        if (orden.Estado == "Terminado") orden.FechaFin = DateTime.Now;
    }

    var avance = new AvanceProduccion
    {
        OrdenProduccionId = orden.Id,
        Fecha = DateTime.Now,
        Estado = orden.Estado,
        CantidadProducida = orden.CantidadProducida,
        Observaciones = observaciones ?? string.Empty
    };

    _context.AvancesProduccion.Add(avance);

    if (orden.Estado == "Terminado" && estadoAnterior != "Terminado")
    {
        var notificacion = new Notificacion
        {
            OrdenProduccionId = orden.Id,
            Mensaje = $"La orden {orden.NumeroOrden} ha terminado su producción y está pendiente de control de calidad.",
            Area = "Distribución",
            Fecha = DateTime.Now,
            Leida = false
        };

        _context.Notificaciones.Add(notificacion);
    }

    await _context.SaveChangesAsync();
    TempData["Ok"] = $"Orden {orden.NumeroOrden} actualizada: {orden.Estado}.";
    return RedirectToAction(nameof(Index));
}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegistrarCalidad(
            InspeccionCalidad inspeccion,
            List<IFormFile> evidencias)
        {
            var orden = await _context.OrdenesProduccion
                .FirstOrDefaultAsync(o => o.Id == inspeccion.OrdenProduccionId);

            if (orden == null)
            {
                TempData["Error"] = "La orden de producción no existe.";
                return RedirectToAction(nameof(Index));
            }

            if (orden.Estado != "Terminado")
            {
                TempData["Error"] =
                    "Solo se puede registrar el control de calidad de una orden terminada.";

                return RedirectToAction(nameof(Index));
            }

            var inspeccionExistente = await _context.InspeccionesCalidad
                .AnyAsync(i => i.OrdenProduccionId == orden.Id);

            if (inspeccionExistente)
            {
                TempData["Error"] =
                    $"La orden {orden.NumeroOrden} ya tiene un control de calidad registrado.";

                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(inspeccion.Lote))
            {
                TempData["Error"] = "Debe indicar el lote inspeccionado.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(inspeccion.Inspector))
            {
                TempData["Error"] = "Debe indicar el inspector.";
                return RedirectToAction(nameof(Index));
            }

            // Validar evidencias antes de guardar
            if (evidencias != null && evidencias.Any())
            {
                string[] extensionesPermitidas =
                {
                    ".jpg",
                    ".jpeg",
                    ".png",
                    ".webp"
                };

                foreach (var archivo in evidencias)
                {
                    if (archivo.Length <= 0)
                        continue;

                    var extension = Path
                        .GetExtension(archivo.FileName)
                        .ToLowerInvariant();

                    if (!extensionesPermitidas.Contains(extension))
                    {
                        TempData["Error"] =
                            "Las evidencias deben ser imágenes JPG, JPEG, PNG o WEBP.";

                        return RedirectToAction(nameof(Index));
                    }

                    if (archivo.Length > 5 * 1024 * 1024)
                    {
                        TempData["Error"] =
                            "Cada fotografía debe pesar como máximo 5 MB.";

                        return RedirectToAction(nameof(Index));
                    }
                }
            }

            inspeccion.Fecha = DateTime.Now;

            _context.InspeccionesCalidad.Add(inspeccion);

            await _context.SaveChangesAsync();

            if (inspeccion.Aprobado)
            {
                var producto = await _context.ProductosTerminados
                    .FirstOrDefaultAsync(p => p.Nombre == orden.TipoCaja);

                if (producto != null)
                {
                    producto.StockActual += orden.CantidadProducida;
                }

                _context.Notificaciones.Add(new Notificacion
                {
                    OrdenProduccionId = orden.Id,
                    Area = "Distribución",
                    Mensaje =
                        $"La orden {orden.NumeroOrden} fue APROBADA en control de calidad y está disponible para distribución.",
                    Fecha = DateTime.Now,
                    Leida = false
                });
            }
            else
            {
                _context.Notificaciones.Add(new Notificacion
                {
                    OrdenProduccionId = orden.Id,
                    Area = "Distribución",
                    Mensaje =
                        $"La orden {orden.NumeroOrden} fue RECHAZADA en control de calidad y no puede pasar a distribución.",
                    Fecha = DateTime.Now,
                    Leida = false
                });
            }

            await _context.SaveChangesAsync();

            if (evidencias != null && evidencias.Any())
            {
                var carpeta = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "calidad");

                Directory.CreateDirectory(carpeta);

                foreach (var archivo in evidencias)
                {
                    if (archivo.Length <= 0)
                        continue;

                    var extension = Path
                        .GetExtension(archivo.FileName)
                        .ToLowerInvariant();

                    var nombreArchivo =
                        $"{Guid.NewGuid()}{extension}";

                    var rutaFisica =
                        Path.Combine(carpeta, nombreArchivo);

                    using var stream =
                        new FileStream(rutaFisica, FileMode.Create);

                    await archivo.CopyToAsync(stream);

                    _context.EvidenciasCalidad.Add(
                        new EvidenciaCalidad
                        {
                            InspeccionCalidadId = inspeccion.Id,

                            RutaArchivo =
                                $"/uploads/calidad/{nombreArchivo}",

                            FechaCarga = DateTime.Now
                        });
                }

                await _context.SaveChangesAsync();
            }

            TempData["Ok"] =
                inspeccion.Aprobado
                    ? $"Control de calidad aprobado para {orden.NumeroOrden}."
                    : $"Control de calidad rechazado para {orden.NumeroOrden}.";

            return RedirectToAction(nameof(Index));
        }
    }
}
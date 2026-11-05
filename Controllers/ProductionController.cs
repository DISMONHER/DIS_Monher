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
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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
                    MaterialId = c.MaterialId,
                    TipoMovimiento = "SALIDA",
                    Cantidad = requerido,
                    Motivo = $"Producción {orden.NumeroOrden}",
                    Responsable = orden.Responsable
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

            if (string.IsNullOrWhiteSpace(inspeccion.Observaciones))
            {
                TempData["Error"] =
                    "Debe completar las observaciones del control de calidad.";

                return RedirectToAction(nameof(Index));
            }

            if (evidencias == null || !evidencias.Any())
            {
                TempData["Error"] = "Debe adjuntar al menos una evidencia fotográfica.";
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

            

            inspeccion.Lote = (inspeccion.Lote ?? string.Empty).Trim();
            inspeccion.Inspector = (inspeccion.Inspector ?? string.Empty).Trim();
            inspeccion.Observaciones = (inspeccion.Observaciones ?? string.Empty).Trim();

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

        // HU009: Reporte de calidad por período
        [HttpGet]
        public async Task<IActionResult> ReporteCalidad(
            DateTime? desde,
            DateTime? hasta)
        {
            var query = _context.InspeccionesCalidad
                .Include(i => i.OrdenProduccion)
                .Include(i => i.Evidencias)
                .AsQueryable();

            if (desde.HasValue)
            {
                query = query.Where(i =>
                    i.Fecha >= desde.Value.Date);
            }

            if (hasta.HasValue)
            {
                var fechaHasta = hasta.Value.Date.AddDays(1);

                query = query.Where(i =>
                    i.Fecha < fechaHasta);
            }

            var inspecciones = await query
                .OrderByDescending(i => i.Fecha)
                .ToListAsync();

            ViewBag.Desde = desde?.ToString("yyyy-MM-dd");
            ViewBag.Hasta = hasta?.ToString("yyyy-MM-dd");

            ViewBag.Total = inspecciones.Count;
            ViewBag.Aprobadas = inspecciones.Count(i => i.Aprobado);
            ViewBag.Rechazadas = inspecciones.Count(i => !i.Aprobado);

            ViewBag.PorcentajeAprobacion =
                inspecciones.Count > 0
                    ? Math.Round(
                        inspecciones.Count(i => i.Aprobado) * 100.0 /
                        inspecciones.Count,
                        2)
                    : 0;

            return View(inspecciones);
        }

        // HU009: Generar reporte de calidad en PDF
        [HttpGet]
        public async Task<IActionResult> GenerarReporteCalidadPdf(
        DateTime? desde,
        DateTime? hasta,
        bool descargar = true)
        {
            var query = _context.InspeccionesCalidad
                .Include(i => i.OrdenProduccion)
                .Include(i => i.Evidencias)
                .AsQueryable();

            if (desde.HasValue)
            {
                query = query.Where(i =>
                    i.Fecha >= desde.Value.Date);
            }

            if (hasta.HasValue)
            {
                var fechaHasta = hasta.Value.Date.AddDays(1);

                query = query.Where(i =>
                    i.Fecha < fechaHasta);
            }

            var inspecciones = await query
                .OrderByDescending(i => i.Fecha)
                .ToListAsync();

            var total = inspecciones.Count;
            var aprobadas = inspecciones.Count(i => i.Aprobado);
            var rechazadas = inspecciones.Count(i => !i.Aprobado);

            var porcentajeAprobacion =
                total > 0
                    ? Math.Round(aprobadas * 100.0 / total, 2)
                    : 0;

            var periodo =
                desde.HasValue || hasta.HasValue
                    ? $"{(desde.HasValue ? desde.Value.ToString("dd/MM/yyyy") : "Inicio")} - {(hasta.HasValue ? hasta.Value.ToString("dd/MM/yyyy") : "Actualidad")}"
                    : "Todos los registros";

            var fechaGeneracion = DateTime.Now;


            static IContainer HeaderCell(IContainer container)
            {
                return container
                    .Background("#163A70")
                    .PaddingVertical(8)
                    .PaddingHorizontal(6)
                    .DefaultTextStyle(x =>
                        x.FontColor(Colors.White)
                        .FontSize(8)
                        .SemiBold());
            }


            static IContainer BodyCell(IContainer container)
            {
                return container
                    .BorderBottom(1)
                    .BorderColor("#E5E7EB")
                    .Background(Colors.White)
                    .PaddingVertical(8)
                    .PaddingHorizontal(6)
                    .DefaultTextStyle(x =>
                        x.FontSize(8)
                        .FontColor("#263238"));
            }


            var document = Document.Create(document =>
            {
                document.Page(page =>
                {
                    // Horizontal para que la tabla se vea mejor
                    page.Size(PageSizes.A4.Landscape());

                    page.Margin(25);

                    page.PageColor("#F5F7FA");

                    page.DefaultTextStyle(x =>
                        x.FontSize(9)
                        .FontColor("#263238"));


                    // ==========================
                    // ENCABEZADO
                    // ==========================
                    page.Header()
                        .PaddingBottom(15)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Column(column =>
                                {
                                    column.Item()
                                        .Text("MONHER E.I.R.L.")
                                        .FontSize(21)
                                        .Bold()
                                        .FontColor("#163A70");

                                    column.Item()
                                        .PaddingTop(3)
                                        .Text("Sistema Integral de Gestión de Producción e Inventario")
                                        .FontSize(9)
                                        .FontColor("#607D8B");
                                });


                            row.ConstantItem(260)
                                .AlignRight()
                                .Column(column =>
                                {
                                    column.Item()
                                        .AlignRight()
                                        .Text("REPORTE DE CONTROL DE CALIDAD")
                                        .FontSize(14)
                                        .Bold()
                                        .FontColor("#163A70");

                                    column.Item()
                                        .PaddingTop(4)
                                        .AlignRight()
                                        .Text($"Generado: {fechaGeneracion:dd/MM/yyyy HH:mm}")
                                        .FontSize(8)
                                        .FontColor("#607D8B");
                                });
                        });


                    // ==========================
                    // CONTENIDO
                    // ==========================
                    page.Content()
                        .Column(column =>
                        {
                            column.Spacing(14);


                            // TÍTULO Y PERÍODO
                            column.Item()
                                .Background(Colors.White)
                                .Border(1)
                                .BorderColor("#E3E8EF")
                                .Padding(14)
                                .Row(row =>
                                {
                                    row.RelativeItem()
                                        .Column(c =>
                                        {
                                            c.Item()
                                                .Text("Resumen del control de calidad")
                                                .FontSize(13)
                                                .Bold()
                                                .FontColor("#1F2937");

                                            c.Item()
                                                .PaddingTop(4)
                                                .Text("Inspecciones realizadas sobre los productos terminados.")
                                                .FontSize(8)
                                                .FontColor("#6B7280");
                                        });


                                    row.ConstantItem(230)
                                        .AlignRight()
                                        .Column(c =>
                                        {
                                            c.Item()
                                                .AlignRight()
                                                .Text("PERÍODO")
                                                .FontSize(7)
                                                .SemiBold()
                                                .FontColor("#6B7280");

                                            c.Item()
                                                .PaddingTop(3)
                                                .AlignRight()
                                                .Text(periodo)
                                                .FontSize(10)
                                                .SemiBold()
                                                .FontColor("#163A70");
                                        });
                                });


                            // ==========================
                            // TARJETAS RESUMEN
                            // ==========================
                            column.Item()
                                .Row(row =>
                                {
                                    row.Spacing(10);


                                    row.RelativeItem()
                                        .Background(Colors.White)
                                        .Border(1)
                                        .BorderColor("#E3E8EF")
                                        .Padding(12)
                                        .Column(c =>
                                        {
                                            c.Item()
                                                .Text("TOTAL INSPECCIONES")
                                                .FontSize(7)
                                                .SemiBold()
                                                .FontColor("#6B7280");

                                            c.Item()
                                                .PaddingTop(5)
                                                .Text(total.ToString())
                                                .FontSize(20)
                                                .Bold()
                                                .FontColor("#1F2937");
                                        });


                                    row.RelativeItem()
                                        .Background(Colors.White)
                                        .Border(1)
                                        .BorderColor("#D1FAE5")
                                        .Padding(12)
                                        .Column(c =>
                                        {
                                            c.Item()
                                                .Text("APROBADAS")
                                                .FontSize(7)
                                                .SemiBold()
                                                .FontColor("#6B7280");

                                            c.Item()
                                                .PaddingTop(5)
                                                .Text(aprobadas.ToString())
                                                .FontSize(20)
                                                .Bold()
                                                .FontColor("#15803D");
                                        });


                                    row.RelativeItem()
                                        .Background(Colors.White)
                                        .Border(1)
                                        .BorderColor("#FECACA")
                                        .Padding(12)
                                        .Column(c =>
                                        {
                                            c.Item()
                                                .Text("RECHAZADAS")
                                                .FontSize(7)
                                                .SemiBold()
                                                .FontColor("#6B7280");

                                            c.Item()
                                                .PaddingTop(5)
                                                .Text(rechazadas.ToString())
                                                .FontSize(20)
                                                .Bold()
                                                .FontColor("#DC2626");
                                        });


                                    row.RelativeItem()
                                        .Background(Colors.White)
                                        .Border(1)
                                        .BorderColor("#DBEAFE")
                                        .Padding(12)
                                        .Column(c =>
                                        {
                                            c.Item()
                                                .Text("PORCENTAJE DE APROBACIÓN")
                                                .FontSize(7)
                                                .SemiBold()
                                                .FontColor("#6B7280");

                                            c.Item()
                                                .PaddingTop(5)
                                                .Text($"{porcentajeAprobacion}%")
                                                .FontSize(20)
                                                .Bold()
                                                .FontColor("#2563EB");
                                        });
                                });


                            // ==========================
                            // TABLA
                            // ==========================
                            column.Item()
                                .Background(Colors.White)
                                .Border(1)
                                .BorderColor("#E3E8EF")
                                .Padding(12)
                                .Column(tableSection =>
                                {
                                    tableSection.Item()
                                        .Text("Detalle de inspecciones")
                                        .FontSize(12)
                                        .Bold()
                                        .FontColor("#1F2937");


                                    tableSection.Item()
                                        .PaddingTop(3)
                                        .PaddingBottom(10)
                                        .Text("Resultados registrados durante el período seleccionado.")
                                        .FontSize(8)
                                        .FontColor("#6B7280");


                                    if (!inspecciones.Any())
                                    {
                                        tableSection.Item()
                                            .PaddingVertical(35)
                                            .AlignCenter()
                                            .Text("No existen inspecciones para el período seleccionado.")
                                            .FontSize(10)
                                            .FontColor("#6B7280");
                                    }
                                    else
                                    {
                                        tableSection.Item()
                                            .Table(table =>
                                            {
                                                table.ColumnsDefinition(columns =>
                                                {
                                                    columns.ConstantColumn(85);
                                                    columns.ConstantColumn(80);
                                                    columns.ConstantColumn(85);
                                                    columns.ConstantColumn(115);
                                                    columns.ConstantColumn(85);
                                                    columns.RelativeColumn(2);
                                                    columns.ConstantColumn(55);
                                                });


                                                table.Header(header =>
                                                {
                                                    header.Cell()
                                                        .Element(HeaderCell)
                                                        .Text("Fecha");

                                                    header.Cell()
                                                        .Element(HeaderCell)
                                                        .Text("Orden");

                                                    header.Cell()
                                                        .Element(HeaderCell)
                                                        .Text("Lote");

                                                    header.Cell()
                                                        .Element(HeaderCell)
                                                        .Text("Inspector");

                                                    header.Cell()
                                                        .Element(HeaderCell)
                                                        .Text("Resultado");

                                                    header.Cell()
                                                        .Element(HeaderCell)
                                                        .Text("Observaciones");

                                                    header.Cell()
                                                        .Element(HeaderCell)
                                                        .AlignCenter()
                                                        .Text("Fotos");
                                                });


                                                foreach (var inspeccion in inspecciones)
                                                {
                                                    table.Cell()
                                                        .Element(BodyCell)
                                                        .Text(inspeccion.Fecha.ToString("dd/MM/yyyy HH:mm"));


                                                    table.Cell()
                                                        .Element(BodyCell)
                                                        .Text(
                                                            inspeccion.OrdenProduccion?.NumeroOrden
                                                            ?? $"#{inspeccion.OrdenProduccionId}");


                                                    table.Cell()
                                                        .Element(BodyCell)
                                                        .Text(
                                                            string.IsNullOrWhiteSpace(inspeccion.Lote)
                                                                ? "-"
                                                                : inspeccion.Lote);


                                                    table.Cell()
                                                        .Element(BodyCell)
                                                        .Text(
                                                            string.IsNullOrWhiteSpace(inspeccion.Inspector)
                                                                ? "-"
                                                                : inspeccion.Inspector);


                                                    if (inspeccion.Aprobado)
                                                    {
                                                        table.Cell()
                                                            .Element(BodyCell)
                                                            .Text("APROBADO")
                                                            .SemiBold()
                                                            .FontColor("#15803D");
                                                    }
                                                    else
                                                    {
                                                        table.Cell()
                                                            .Element(BodyCell)
                                                            .Text("RECHAZADO")
                                                            .SemiBold()
                                                            .FontColor("#DC2626");
                                                    }


                                                    table.Cell()
                                                        .Element(BodyCell)
                                                        .Text(
                                                            string.IsNullOrWhiteSpace(inspeccion.Observaciones)
                                                                ? "-"
                                                                : inspeccion.Observaciones);


                                                    table.Cell()
                                                        .Element(BodyCell)
                                                        .AlignCenter()
                                                        .Text(
                                                            inspeccion.Evidencias?.Count.ToString()
                                                            ?? "0");
                                                }
                                            });
                                    }
                                });
                        });


                    // ==========================
                    // PIE DE PÁGINA
                    // ==========================
                    page.Footer()
                        .PaddingTop(10)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text("MONHER E.I.R.L. · Reporte de Control de Calidad")
                                .FontSize(7)
                                .FontColor("#78909C");


                            row.RelativeItem()
                                .AlignRight()
                                .Text(text =>
                                {
                                    text.DefaultTextStyle(x =>
                                        x.FontSize(7)
                                        .FontColor("#78909C"));

                                    text.Span("Página ");
                                    text.CurrentPageNumber();
                                    text.Span(" de ");
                                    text.TotalPages();
                                });
                        });
                });
            });


            var pdf = document.GeneratePdf();

            var nombreArchivo =
                $"Reporte_Calidad_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

            if (descargar)
            {
                return File(
                    pdf,
                    "application/pdf",
                    nombreArchivo);
            }

            // Abrir el PDF directamente en el navegador para imprimir
            return File(
                pdf,
                "application/pdf");
                    }

    }
}
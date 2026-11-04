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
    public class SalesController : Controller
    {
        private readonly ApplicationDbContext _context;
        public SalesController(ApplicationDbContext context) => _context = context;

        // HU011: Registrar Clientes y Pedidos
        public async Task<IActionResult> Pedidos()
{
    ViewBag.Clientes = await _context.Clientes.ToListAsync();
    ViewBag.Productos = await _context.ProductosTerminados.ToListAsync();
    return View(await _context.Pedidos.Include(p => p.Cliente).Include(p => p.Detalles).ThenInclude(d => d.ProductoTerminado)
        .OrderByDescending(p => p.Id).ToListAsync());
}

[HttpPost]
public async Task<IActionResult> CrearCliente(Cliente cliente)
{
    if (await _context.Clientes.AnyAsync(c => c.Documento == cliente.Documento))
    {
        TempData["Error"] = "Ya existe un cliente con ese documento.";
        return RedirectToAction(nameof(Pedidos));
    }
    cliente.TipoCliente = cliente.Documento.Length == 11 ? "Empresa" : "Persona";
    _context.Clientes.Add(cliente);
    await _context.SaveChangesAsync();
    TempData["Ok"] = "Cliente registrado.";
    return RedirectToAction(nameof(Pedidos));
}

// El precio se toma de la BD (antes venía del navegador)
[HttpPost]
public async Task<IActionResult> CrearPedido(int clienteId, DateTime fechaEntrega, int[]? productoIds, int[]? cantidades)
{
    if (productoIds == null || cantidades == null || productoIds.Length == 0 ||
        !await _context.Clientes.AnyAsync(c => c.Id == clienteId))
    {
        TempData["Error"] = "Seleccione un cliente y al menos un producto.";
        return RedirectToAction(nameof(Pedidos));
    }

    var productos = await _context.ProductosTerminados.Where(p => productoIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
    var pedido = new Pedido { CodigoPedido = "TEMP", ClienteId = clienteId, FechaEntrega = fechaEntrega, Estado = "Registrado" };

    for (int i = 0; i < productoIds.Length && i < cantidades.Length; i++)
    {
        if (cantidades[i] <= 0 || !productos.TryGetValue(productoIds[i], out var prod)) continue;
        pedido.Detalles.Add(new DetallePedido { ProductoTerminadoId = prod.Id, Cantidad = cantidades[i], Precio = prod.PrecioUnitario });
    }
    if (!pedido.Detalles.Any())
    {
        TempData["Error"] = "El pedido no tiene productos válidos.";
        return RedirectToAction(nameof(Pedidos));
    }

    pedido.Total = pedido.Detalles.Sum(d => d.Cantidad * d.Precio);
    _context.Pedidos.Add(pedido);
    await _context.SaveChangesAsync();
    pedido.CodigoPedido = $"PED-{pedido.Id:D5}";
    await _context.SaveChangesAsync();

    return RedirectToAction(nameof(VerificarDisponibilidad), new { id = pedido.Id });
}

public async Task<IActionResult> VerificarDisponibilidad(int id)
{
    var pedido = await _context.Pedidos.Include(p => p.Cliente).Include(p => p.Detalles).ThenInclude(d => d.ProductoTerminado)
        .FirstOrDefaultAsync(p => p.Id == id);
    if (pedido == null) return NotFound();

    // Reserva real: lo que otros pedidos "Confirmado" ya comprometieron
    var ids = pedido.Detalles.Select(d => d.ProductoTerminadoId).ToList();
    var reservados = await _context.DetallesPedido
        .Where(d => d.PedidoId != id && d.Pedido!.Estado == "Confirmado" && ids.Contains(d.ProductoTerminadoId))
        .GroupBy(d => d.ProductoTerminadoId)
        .Select(g => new { g.Key, Total = g.Sum(x => x.Cantidad) })
        .ToDictionaryAsync(x => x.Key, x => x.Total);
    ViewBag.Reservados = reservados;
    return View(pedido);
}

[HttpPost]
public async Task<IActionResult> ConfirmarReserva(int id)
{
    var pedido = await _context.Pedidos.Include(p => p.Detalles).ThenInclude(d => d.ProductoTerminado)
        .FirstOrDefaultAsync(p => p.Id == id);
    if (pedido == null) return NotFound();
    if (pedido.Estado != "Registrado") return RedirectToAction(nameof(Pedidos));

    foreach (var d in pedido.Detalles)
    {
        var reservado = await _context.DetallesPedido
            .Where(x => x.PedidoId != id && x.Pedido!.Estado == "Confirmado" && x.ProductoTerminadoId == d.ProductoTerminadoId)
            .SumAsync(x => x.Cantidad);
        if (d.ProductoTerminado!.StockActual - reservado < d.Cantidad)
        {
            TempData["Error"] = $"Stock insuficiente de {d.ProductoTerminado.Nombre}.";
            return RedirectToAction(nameof(VerificarDisponibilidad), new { id });
        }
    }

    pedido.Estado = "Confirmado";
    await _context.SaveChangesAsync();
    TempData["Ok"] = "Stock reservado. Continúe con el despacho.";
    return RedirectToAction(nameof(DespachoView), new { id });
}
        // PANEL GENERAL DE DESPACHOS
        public async Task<IActionResult> Despachos()
        {
            ViewBag.PedidosPendientes = await _context.Pedidos
                .Include(p => p.Cliente)
                .Where(p => p.Estado == "Confirmado")
                .OrderBy(p => p.FechaEntrega)
                .ToListAsync();

            return View(await _context.Despachos
                .Include(d => d.Pedido)
                .ThenInclude(p => p!.Cliente)
                .OrderByDescending(d => d.FechaDespacho)
                .ToListAsync());
        }

        // FORMULARIO PARA PROGRAMAR DESPACHO
        public async Task<IActionResult> DespachoView(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Cliente)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
                return NotFound();

            if (pedido.Estado != "Confirmado")
            {
                TempData["Error"] =
                    "El pedido debe estar confirmado para programar su despacho.";

                return RedirectToAction(nameof(Despachos));
            }

            return View(pedido);
        }

        // REGISTRAR SALIDA Y DESCONTAR STOCK
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProgramarDespacho(
            int pedidoId, string transportista)
        {
            if (string.IsNullOrWhiteSpace(transportista) ||
                transportista.Trim().Length > 150)
            {
                TempData["Error"] =
                    "Ingrese un transportista válido (máximo 150 caracteres).";

                return RedirectToAction(
                    nameof(DespachoView), new { id = pedidoId });
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            var pedido = await _context.Pedidos
                .Include(p => p.Detalles)
                .ThenInclude(d => d.ProductoTerminado)
                .FirstOrDefaultAsync(p => p.Id == pedidoId);

            if (pedido == null)
                return NotFound();

            if (pedido.Estado != "Confirmado")
            {
                TempData["Error"] =
                    "Solo se puede despachar un pedido con stock confirmado.";

                return RedirectToAction(nameof(Pedidos));
            }

            if (await _context.Despachos
                .AnyAsync(d => d.PedidoId == pedidoId))
            {
                TempData["Error"] =
                    "Este pedido ya tiene un despacho registrado.";

                return RedirectToAction(nameof(Despachos));
            }

            // Comprobar stock agrupando cantidades por producto.
            var cantidades = pedido.Detalles
                .GroupBy(d => d.ProductoTerminadoId)
                .Select(g => new
                {
                    ProductoId = g.Key,
                    Cantidad = g.Sum(d => d.Cantidad)
                })
                .ToList();

            var productos = pedido.Detalles
                .Where(d => d.ProductoTerminado != null)
                .Select(d => d.ProductoTerminado!)
                .DistinctBy(p => p.Id)
                .ToDictionary(p => p.Id);

            foreach (var item in cantidades)
            {
                if (!productos.TryGetValue(item.ProductoId, out var producto) ||
                    producto.StockActual < item.Cantidad)
                {
                    TempData["Error"] =
                        "El stock físico no alcanza para despachar este pedido.";

                    return RedirectToAction(
                        nameof(VerificarDisponibilidad),
                        new { id = pedidoId });
                }
            }

            // Descontar existencias físicas.
            foreach (var item in cantidades)
            {
                productos[item.ProductoId].StockActual -= item.Cantidad;
            }

            pedido.Estado = "Despachado";

            _context.Despachos.Add(new Despacho
            {
                PedidoId = pedidoId,
                Transportista = transportista.Trim(),
                FechaDespacho = DateTime.Now,
                EstadoEntrega = "En Camino"
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["Ok"] =
                "Despacho registrado y stock descontado.";

            return RedirectToAction(
                nameof(Facturacion), new { id = pedidoId });
        }

        // CONFIRMAR ENTREGA AL CLIENTE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarEntrega(int despachoId)
        {
            var despacho = await _context.Despachos
                .FindAsync(despachoId);

            if (despacho == null)
                return NotFound();

            if (despacho.EstadoEntrega != "Entregado")
            {
                despacho.EstadoEntrega = "Entregado";
                despacho.FechaEntregaReal = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Ok"] =
                    "Entrega confirmada correctamente.";
            }

            return RedirectToAction(nameof(Despachos));
        }

public async Task<IActionResult> Facturacion(int id)
{
    var pedido = await _context.Pedidos.Include(p => p.Cliente).Include(p => p.Detalles).ThenInclude(d => d.ProductoTerminado)
        .FirstOrDefaultAsync(p => p.Id == id);
    if (pedido == null) return NotFound();
    return View(pedido);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> EmitirComprobante(
    int pedidoId,
    string tipoComprobante,
    string metodoPago)
{
    if (tipoComprobante != "Boleta" &&
        tipoComprobante != "Factura")
    {
        TempData["Error"] = "Seleccione un tipo de comprobante válido.";
        return RedirectToAction(nameof(Facturacion),
            new { id = pedidoId });
    }

    if (string.IsNullOrWhiteSpace(metodoPago) ||
        !new[] {
            "Transferencia Bancaria",
            "Efectivo",
            "Tarjeta de Crédito"
        }.Contains(metodoPago))
    {
        TempData["Error"] = "Seleccione un método de pago válido.";
        return RedirectToAction(nameof(Facturacion),
            new { id = pedidoId });
    }

    await using var transaction =
        await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);

    var pedido = await _context.Pedidos
        .Include(p => p.Cliente)
        .FirstOrDefaultAsync(p => p.Id == pedidoId);

    if (pedido == null)
        return NotFound();

    var existente = await _context.ComprobantesVenta
        .FirstOrDefaultAsync(c => c.PedidoId == pedidoId);

    if (existente != null)
    {
        return RedirectToAction(nameof(VerComprobante),
            new { id = existente.Id });
    }

    if (pedido.Estado != "Despachado")
    {
        TempData["Error"] =
            "Solo se puede facturar un pedido despachado.";

        return RedirectToAction(nameof(Pedidos));
    }

    if (tipoComprobante == "Factura" &&
        (pedido.Cliente?.Documento?.Length != 11 ||
         !pedido.Cliente.Documento.All(char.IsDigit)))
    {
        TempData["Error"] =
            "Para emitir una factura se requiere un RUC de 11 dígitos.";

        return RedirectToAction(nameof(Facturacion),
            new { id = pedidoId });
    }

    string serie = tipoComprobante == "Factura"
        ? "F001"
        : "B001";

    var numeros = await _context.ComprobantesVenta
        .Where(c => c.TipoComprobante == tipoComprobante)
        .Select(c => c.NumeroComprobante)
        .ToListAsync();

    int ultimo = numeros
        .Where(n => n != null && n.StartsWith(serie + "-"))
        .Select(n => int.TryParse(n!.Substring(5), out int valor)
            ? valor : 0)
        .DefaultIfEmpty(0)
        .Max();

    int correlativo = ultimo + 1;

    decimal subtotal = Math.Round(
        pedido.Total / 1.18m, 2);

    decimal igv = pedido.Total - subtotal;

    var comprobante = new ComprobanteVenta
    {
        PedidoId = pedido.Id,
        NumeroComprobante = $"{serie}-{correlativo:D7}",
        TipoComprobante = tipoComprobante,
        Subtotal = subtotal,
        IGV = igv,
        Total = pedido.Total,
        MetodoPago = metodoPago,
        FechaEmision = DateTime.Now
    };

    _context.ComprobantesVenta.Add(comprobante);

    pedido.Estado = "Vendido";

    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    TempData["Ok"] =
        "Comprobante generado correctamente.";

    return RedirectToAction(nameof(VerComprobante),
        new { id = comprobante.Id });
}

// MOSTRAR COMPROBANTE GENERADO
[HttpGet]
public async Task<IActionResult> VerComprobante(int id)
{
    var comprobante = await _context.ComprobantesVenta
        .Include(c => c.Pedido)
            .ThenInclude(p => p!.Cliente)
        .Include(c => c.Pedido)
            .ThenInclude(p => p!.Detalles)
                .ThenInclude(d => d.ProductoTerminado)
        .FirstOrDefaultAsync(c => c.Id == id);

    if (comprobante == null)
        return NotFound();

    return View(comprobante);
}

        // HU015: Reporte de Ventas y Distribución
        public async Task<IActionResult> ReporteVentas()
{
    var comprobantes = await _context.ComprobantesVenta.Include(c => c.Pedido).ThenInclude(p => p!.Cliente)
        .OrderByDescending(c => c.FechaEmision).ToListAsync();
    var despachos = await _context.Despachos.Include(d => d.Pedido).ThenInclude(p => p!.Cliente)
        .OrderByDescending(d => d.FechaDespacho).ToListAsync();
    var ordenes = await _context.OrdenesProduccion.Where(o => o.Estado != "Cancelado").ToListAsync();
    var movs = await _context.MovimientosInventario.ToListAsync();
    var materiales = await _context.Materiales.ToListAsync();

    var iniMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
    var ventasMes = comprobantes.Where(c => c.FechaEmision >= iniMes).Sum(c => c.Total);
    var ventasPrev = comprobantes.Where(c => c.FechaEmision >= iniMes.AddMonths(-1) && c.FechaEmision < iniMes).Sum(c => c.Total);

    var planificado = ordenes.Sum(o => o.Cantidad);
    var salidas = movs.Where(m => m.TipoMovimiento == "SALIDA").Sum(m => m.Cantidad);
    var mermas = movs.Where(m => m.TipoMovimiento == "SALIDA" && m.Motivo.Contains("merma", StringComparison.OrdinalIgnoreCase)).Sum(m => m.Cantidad);
    var stockTotal = materiales.Sum(m => m.StockActual);
    var terminadas = ordenes.Where(o => o.Estado == "Terminado" && o.FechaFin.HasValue).ToList();

    var vm = new ReporteVM
    {
        VentasMes = ventasMes,
        VariacionVentas = ventasPrev > 0 ? Math.Round((ventasMes - ventasPrev) * 100 / ventasPrev, 1) : null,
        TotalComprobantes = comprobantes.Count,
        TicketPromedio = comprobantes.Any() ? Math.Round(comprobantes.Average(c => c.Total), 2) : 0,
        PedidosEnCurso = await _context.Pedidos.CountAsync(p => p.Estado != "Vendido"),
        MaterialesCriticos = materiales.Count(m => m.StockActual <= m.StockMinimo),
        EficienciaProduccion = planificado > 0 ? Math.Round(ordenes.Sum(o => o.CantidadProducida) * 100m / planificado, 1) : 0,
        TasaMermas = salidas > 0 ? Math.Round(mermas * 100 / salidas, 2) : 0,
        RotacionInventario = stockTotal > 0 ? Math.Round(salidas / stockTotal, 2) : 0,
        CicloProduccionDias = terminadas.Any() ? Math.Round((decimal)terminadas.Average(o => (o.FechaFin!.Value - o.FechaCreacion).TotalDays), 1) : 0,
        Comprobantes = comprobantes,
        Despachos = despachos
    };
    return View(vm);
}
    }
}
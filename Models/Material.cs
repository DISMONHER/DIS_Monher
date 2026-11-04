using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DIS_Monher.Models
{
    public class Material
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Codigo { get; set; } = string.Empty;
        [Required]
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string UnidadMedida { get; set; } = string.Empty;
        public decimal StockActual { get; set; } = 0;
        public decimal StockMinimo { get; set; } = 0;
        public string Almacen { get; set; } = "Almacén Principal";
        public DateTime FechaIngreso { get; set; } = DateTime.Now;
        public string UsuarioResponsable { get; set; } = "Sistema";
    }

    public class MovimientoInventario
    {
        [Key]
        public int Id { get; set; }
        public int MaterialId { get; set; }
        public Material? Material { get; set; }
        public string TipoMovimiento { get; set; } = "ENTRADA"; // ENTRADA / SALIDA
        public decimal Cantidad { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Responsable { get; set; } = string.Empty;
    }

    public class ProductoTerminado
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Codigo { get; set; } = string.Empty;
        [Required]
        public string Nombre { get; set; } = string.Empty;
        public string TipoCaja { get; set; } = string.Empty;
        public decimal StockActual { get; set; } = 0;
        public decimal PrecioUnitario { get; set; } = 0;
        public List<ProductoMaterial> Composicion { get; set; } = new();
    }

    public class ProductoMaterial
    {
        public int ProductoTerminadoId { get; set; }
        public ProductoTerminado? ProductoTerminado { get; set; }
        public int MaterialId { get; set; }
        public Material? Material { get; set; }
        public decimal CantidadRequerida { get; set; }
    }

    public class AlertaStock
    {
        [Key]
        public int Id { get; set; }
        public int MaterialId { get; set; }
        public Material? Material { get; set; }
        public decimal CantidadActual { get; set; }
        public decimal CantidadSugeridaCompra { get; set; }
        public DateTime FechaGeneracion { get; set; } = DateTime.Now;
        public bool Atendida { get; set; } = false;
    }

    public class OrdenProduccion
    {
        [Key]
        public int Id { get; set; }
        public string NumeroOrden { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public DateTime FechaEntrega { get; set; }
        public DateTime? FechaFin { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string TipoCaja { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public int CantidadProducida { get; set; } = 0;
        public string Estado { get; set; } = "Pendiente"; // Pendiente, En Proceso, Terminado, Cancelado
        public string Responsable { get; set; } = string.Empty;
        public List<DetalleOrdenMaterial> MaterialesAsignados { get; set; } = new();
    }

    public class DetalleOrdenMaterial
    {
        public int OrdenProduccionId { get; set; }
        public OrdenProduccion? OrdenProduccion { get; set; }
        public int MaterialId { get; set; }
        public Material? Material { get; set; }
        public decimal Cantidad { get; set; }
    }

    public class InspeccionCalidad
    {
        [Key]
        public int Id { get; set; }
        public int OrdenProduccionId { get; set; }
        public OrdenProduccion? OrdenProduccion { get; set; }
        public string Lote { get; set; } = string.Empty;
        public string Inspector { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.Now;
        public bool Aprobado { get; set; }
        public string Observaciones { get; set; } = string.Empty;
    }

    public class Cliente
    {
        [Key]
        public int Id { get; set; }
        public string Documento { get; set; } = string.Empty; // RUC/DNI
        public string RazonSocial { get; set; } = string.Empty;
        public string Contacto { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string TipoCliente { get; set; } = string.Empty;
    }

    public class Pedido
    {
        [Key]
        public int Id { get; set; }
        public string CodigoPedido { get; set; } = string.Empty;
        public int ClienteId { get; set; }
        public Cliente? Cliente { get; set; }
        public DateTime FechaEntrega { get; set; }
        public string Estado { get; set; } = "Registrado"; // Registrado, Confirmado, Despachado, Vendido
        public decimal Total { get; set; }
        public List<DetallePedido> Detalles { get; set; } = new();
    }

    public class DetallePedido
    {
        [Key]
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public Pedido? Pedido { get; set; } 
        public int ProductoTerminadoId { get; set; }
        public ProductoTerminado? ProductoTerminado { get; set; }
        public int Cantidad { get; set; }
        public decimal Precio { get; set; }
        public string Personalizacion { get; set; } = string.Empty;
    }

    public class Despacho
    {
        [Key]
        public int Id { get; set; }
        public int PedidoId { get; set; }

        public Pedido? Pedido { get; set; }
        public string Transportista { get; set; } = string.Empty;
        public DateTime FechaDespacho { get; set; } = DateTime.Now;
        public DateTime? FechaEntregaReal { get; set; }
        public string EstadoEntrega { get; set; } = "En Camino";
        public string EvidenciaFirma { get; set; } = string.Empty;
    }

    public class ComprobanteVenta
    {
        [Key]
        public int Id { get; set; }
        public int PedidoId { get; set; }
        public Pedido? Pedido { get; set; }
        public string NumeroComprobante { get; set; } = string.Empty; // B001-0000891
        public string TipoComprobante { get; set; } = "Boleta"; // Boleta / Factura
        public decimal Subtotal { get; set; }
        public decimal IGV { get; set; }
        public decimal Total { get; set; }
        public string MetodoPago { get; set; } = "Transferencia";
        public DateTime FechaEmision { get; set; } = DateTime.Now;
    }
}
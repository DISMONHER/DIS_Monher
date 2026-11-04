using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DIS_Monher.Models
{
    public class ReporteVM
    {
        public decimal VentasMes { get; set; }
        public decimal? VariacionVentas { get; set; }
        public int TotalComprobantes { get; set; }
        public decimal TicketPromedio { get; set; }
        public int PedidosEnCurso { get; set; }
        public int MaterialesCriticos { get; set; }
        public decimal EficienciaProduccion { get; set; }
        public decimal TasaMermas { get; set; }
        public decimal RotacionInventario { get; set; }
        public decimal CicloProduccionDias { get; set; }
        public List<ComprobanteVenta> Comprobantes { get; set; } = new();
        public List<Despacho> Despachos { get; set; } = new();
    }
}
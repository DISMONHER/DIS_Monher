using System.ComponentModel.DataAnnotations;

namespace DIS_Monher.Models
{
    public class Notificacion
    {
        [Key]
        public int Id { get; set; }

        // Para notificaciones relacionadas con producción/calidad
        public int? OrdenProduccionId { get; set; }

        public OrdenProduccion? OrdenProduccion { get; set; }

        // HU012: notificaciones originadas desde un pedido
        public int? PedidoId { get; set; }

        public Pedido? Pedido { get; set; }

        public string Mensaje { get; set; } = string.Empty;

        public string Area { get; set; } = "Distribución";

        public DateTime Fecha { get; set; } = DateTime.Now;

        public bool Leida { get; set; } = false;
    }
}
using System.ComponentModel.DataAnnotations;

namespace DIS_Monher.Models
{
    public class AvanceProduccion
    {
        [Key]
        public int Id { get; set; }

        public int OrdenProduccionId { get; set; }

        public OrdenProduccion? OrdenProduccion { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;

        [Required]
        public string Estado { get; set; } = "En Proceso";

        [Required]
        public int CantidadProducida { get; set; }

        public string Observaciones { get; set; } = string.Empty;
    }
}
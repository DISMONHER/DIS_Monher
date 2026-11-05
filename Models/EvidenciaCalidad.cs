using System.ComponentModel.DataAnnotations;

namespace DIS_Monher.Models
{
    public class EvidenciaCalidad
    {
        [Key]
        public int Id { get; set; }

        public int InspeccionCalidadId { get; set; }

        public InspeccionCalidad? InspeccionCalidad { get; set; }

        public string RutaArchivo { get; set; } = string.Empty;

        public DateTime FechaCarga { get; set; } = DateTime.Now;
    }
}
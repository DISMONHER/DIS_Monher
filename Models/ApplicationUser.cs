using Microsoft.AspNetCore.Identity;

namespace DIS_Monher.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string NombreCompleto { get; set; }
        public string DniRuc { get; set; }
        public int Edad { get; set; }
        public string Direccion { get; set; }
    }
}
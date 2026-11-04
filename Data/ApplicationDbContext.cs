using DIS_Monher.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DIS_Monher.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Material> Materiales { get; set; }
    public DbSet<MovimientoInventario> MovimientosInventario { get; set; }
    public DbSet<ProductoTerminado> ProductosTerminados { get; set; }
    public DbSet<ProductoMaterial> ProductoMateriales { get; set; }
    public DbSet<AlertaStock> AlertasStock { get; set; }
    public DbSet<OrdenProduccion> OrdenesProduccion { get; set; }
    public DbSet<DetalleOrdenMaterial> DetalleOrdenMateriales { get; set; }
    public DbSet<InspeccionCalidad> InspeccionesCalidad { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Pedido> Pedidos { get; set; }
    public DbSet<DetallePedido> DetallesPedido { get; set; }
    public DbSet<Despacho> Despachos { get; set; }
    public DbSet<ComprobanteVenta> ComprobantesVenta { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Usamos convenciones de EF en lugar de tipos de columna explícitos
        // para evitar conflictos de conversión Int32 <-> Decimal

        modelBuilder.Entity<Material>(mb =>
        {
            mb.HasIndex(m => m.Codigo).IsUnique();
        });

        modelBuilder.Entity<ProductoMaterial>()
            .HasKey(pm => new { pm.ProductoTerminadoId, pm.MaterialId });

        modelBuilder.Entity<DetalleOrdenMaterial>()
            .HasKey(dom => new { dom.OrdenProduccionId, dom.MaterialId });

            
        }
}

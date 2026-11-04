// Data/DbInitializer.cs
using DIS_Monher.Models;
using Microsoft.EntityFrameworkCore;

namespace DIS_Monher.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            if (context.Materiales.Any())
            {
                return; // La base de datos ya fue poblada
            }

            var materiales = new Material[]
            {
                new Material { Codigo = "CAN-001", Nombre = "Nueces de Brasil", Descripcion = "Nueces seleccionadas de primera calidad", Categoria = "Alimentos", UnidadMedida = "kg", StockActual = 50, StockMinimo = 20, Almacen = "Almacén Principal", UsuarioResponsable = "Administrador" },
                new Material { Codigo = "CAN-002", Nombre = "Chocolates Variados", Descripcion = "Assortment of premium dark and milk chocolates", Categoria = "Alimentos", UnidadMedida = "un", StockActual = 100, StockMinimo = 30, Almacen = "Almacén Principal", UsuarioResponsable = "Administrador" },
                new Material { Codigo = "CAN-003", Nombre = "Pan Dulce", Descripcion = "Pan dulce tradicional navideño", Categoria = "Alimentos", UnidadMedida = "kg", StockActual = 40, StockMinimo = 15, Almacen = "Almacén Principal", UsuarioResponsable = "Administrador" },
                new Material { Codigo = "CAN-004", Nombre = "Sidra Premium", Descripcion = "Sidra de uva premium para brindis", Categoria = "Alcohólicas", UnidadMedida = "botella", StockActual = 25, StockMinimo = 10, Almacen = "Almacén Principal", UsuarioResponsable = "Administrador" },
                new Material { Codigo = "CAN-005", Nombre = "Lazos Decorativos", Descripcion = "Lazos de satén para decoración de canastas", Categoria = "Decoración", UnidadMedida = "docena", StockActual = 80, StockMinimo = 25, Almacen = "Almacén Principal", UsuarioResponsable = "Administrador" },
                new Material { Codigo = "CAN-006", Nombre = "Papel de Regalo", Descripcion = "Papel de regalo festivo con diseños navideños", Categoria = "Decoración", UnidadMedida = "rollo", StockActual = 200, StockMinimo = 50, Almacen = "Almacén Principal", UsuarioResponsable = "Administrador" },
                new Material { Codigo = "CAN-007", Nombre = "Vasos de Cristal", Descripcion = "Vasos tallados para cócteles y bebidas", Categoria = "Decoración", UnidadMedida = "par", StockActual = 30, StockMinimo = 15, Almacen = "Almacén Principal", UsuarioResponsable = "Administrador" },
                new Material { Codigo = "CAN-008", Nombre = "Figuras de Cerámica", Descripcion = "Figuras artesanales de cerámica para decoración", Categoria = "Manualidades", UnidadMedida = "un", StockActual = 60, StockMinimo = 20, Almacen = "Almacén Principal", UsuarioResponsable = "Administrador" },
                new Material { Codigo = "CAN-009", Nombre = "Miel de Regalo", Descripcion = "Miel en frasco pequeño para regalo", Categoria = "Alimentos", UnidadMedida = "frasco", StockActual = 15, StockMinimo = 10, Almacen = "Almacén Principal", UsuarioResponsable = "Administrador" }
            };
            context.Materiales.AddRange(materiales);
            context.SaveChanges();

            var productos = new ProductoTerminado[]
            {
                new ProductoTerminado { Codigo = "PROD-001", Nombre = "Canasta Básica Navideña", TipoCaja = "Canasta", StockActual = 20, PrecioUnitario = 150.00m },
                new ProductoTerminado { Codigo = "PROD-002", Nombre = "Pack Regalo Ejecutivo", TipoCaja = "Caja", StockActual = 10, PrecioUnitario = 250.00m },
                new ProductoTerminado { Codigo = "PROD-003", Nombre = "Set Decoración Mesa", TipoCaja = "Set", StockActual = 15, PrecioUnitario = 180.00m }
            };
            context.ProductosTerminados.AddRange(productos);
            context.SaveChanges();

            // Composición (receta) por unidad producida - relacioando materiales con productos navideños
            context.ProductoMateriales.AddRange(
                new ProductoMaterial { ProductoTerminadoId = productos[0].Id, MaterialId = materiales[0].Id, CantidadRequerida = 0.5m },  // Canasta: 0.5kg nueces
                new ProductoMaterial { ProductoTerminadoId = productos[0].Id, MaterialId = materiales[1].Id, CantidadRequerida = 2 },    // Canasta: 2 chocolates
                new ProductoMaterial { ProductoTerminadoId = productos[0].Id, MaterialId = materiales[2].Id, CantidadRequerida = 1 },     // Canasta: 1kg pan dulce
                new ProductoMaterial { ProductoTerminadoId = productos[0].Id, MaterialId = materiales[3].Id, CantidadRequerida = 1 },     // Canasta: 1 sidra
                new ProductoMaterial { ProductoTerminadoId = productos[0].Id, MaterialId = materiales[4].Id, CantidadRequerida = 0.5m },    // Canasta: 0.5 docena lazos
                new ProductoMaterial { ProductoTerminadoId = productos[0].Id, MaterialId = materiales[5].Id, CantidadRequerida = 0.2m },    // Canasta: 0.2 rollo papel
                new ProductoMaterial { ProductoTerminadoId = productos[1].Id, MaterialId = materiales[4].Id, CantidadRequerida = 1 },      // Pack: 1 docena lazos
                new ProductoMaterial { ProductoTerminadoId = productos[1].Id, MaterialId = materiales[5].Id, CantidadRequerida = 0.5m },    // Pack: 0.5 rollo papel
                new ProductoMaterial { ProductoTerminadoId = productos[1].Id, MaterialId = materiales[6].Id, CantidadRequerida = 1 },      // Pack: 1 par vasos
                new ProductoMaterial { ProductoTerminadoId = productos[2].Id, MaterialId = materiales[7].Id, CantidadRequerida = 2 },       // Set: 2 figuras cerámica
                new ProductoMaterial { ProductoTerminadoId = productos[2].Id, MaterialId = materiales[5].Id, CantidadRequerida = 0.1m }     // Set: 0.1 rollo papel
            );
            context.SaveChanges();

            var cliente = new Cliente
            {
                Documento = "41882736",
                RazonSocial = "Gloria Paredes",
                Contacto = "+51 987654321",
                Direccion = "Av. Javier Prado Este 1230, San Borja, Lima",
                TipoCliente = "VIP"
            };
            context.Clientes.Add(cliente);
            context.SaveChanges();

            // Alertas de Stock Mínimo iniciales (HU005)
            foreach (var m in materiales)
            {
                InventarioHelper.EvaluarAlerta(context, m);
                context.SaveChanges();
            }
        }
    }
}
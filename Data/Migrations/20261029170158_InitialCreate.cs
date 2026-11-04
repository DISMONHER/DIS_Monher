using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DIS_Monher.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Documento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RazonSocial = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contacto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Direccion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoCliente = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materiales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnidadMedida = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StockActual = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StockMinimo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Almacen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioResponsable = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materiales", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrdenesProduccion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumeroOrden = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaEntrega = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Cliente = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoCaja = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    CantidadProducida = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Responsable = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdenesProduccion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductosTerminados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoCaja = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StockActual = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductosTerminados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoPedido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClienteId = table.Column<int>(type: "int", nullable: false),
                    FechaEntrega = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pedidos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertasStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    CantidadActual = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CantidadSugeridaCompra = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FechaGeneracion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Atendida = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertasStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertasStock_Materiales_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materiales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    TipoMovimiento = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Responsable = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosInventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosInventario_Materiales_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materiales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DetalleOrdenMateriales",
                columns: table => new
                {
                    OrdenProduccionId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetalleOrdenMateriales", x => new { x.OrdenProduccionId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_DetalleOrdenMateriales_Materiales_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materiales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetalleOrdenMateriales_OrdenesProduccion_OrdenProduccionId",
                        column: x => x.OrdenProduccionId,
                        principalTable: "OrdenesProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InspeccionesCalidad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrdenProduccionId = table.Column<int>(type: "int", nullable: false),
                    Lote = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Inspector = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aprobado = table.Column<bool>(type: "bit", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspeccionesCalidad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspeccionesCalidad_OrdenesProduccion_OrdenProduccionId",
                        column: x => x.OrdenProduccionId,
                        principalTable: "OrdenesProduccion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductoMateriales",
                columns: table => new
                {
                    ProductoTerminadoId = table.Column<int>(type: "int", nullable: false),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    CantidadRequerida = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductoMateriales", x => new { x.ProductoTerminadoId, x.MaterialId });
                    table.ForeignKey(
                        name: "FK_ProductoMateriales_Materiales_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materiales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductoMateriales_ProductosTerminados_ProductoTerminadoId",
                        column: x => x.ProductoTerminadoId,
                        principalTable: "ProductosTerminados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComprobantesVenta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    NumeroComprobante = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoComprobante = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IGV = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MetodoPago = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaEmision = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprobantesVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprobantesVenta_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Despachos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    Transportista = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FechaDespacho = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaEntregaReal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstadoEntrega = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenciaFirma = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Despachos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Despachos_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DetallesPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    ProductoTerminadoId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Precio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Personalizacion = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DetallesPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetallesPedido_ProductosTerminados_ProductoTerminadoId",
                        column: x => x.ProductoTerminadoId,
                        principalTable: "ProductosTerminados",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertasStock_MaterialId",
                table: "AlertasStock",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprobantesVenta_PedidoId",
                table: "ComprobantesVenta",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Despachos_PedidoId",
                table: "Despachos",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetalleOrdenMateriales_MaterialId",
                table: "DetalleOrdenMateriales",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_PedidoId",
                table: "DetallesPedido",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesPedido_ProductoTerminadoId",
                table: "DetallesPedido",
                column: "ProductoTerminadoId");

            migrationBuilder.CreateIndex(
                name: "IX_InspeccionesCalidad_OrdenProduccionId",
                table: "InspeccionesCalidad",
                column: "OrdenProduccionId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosInventario_MaterialId",
                table: "MovimientosInventario",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ClienteId",
                table: "Pedidos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoMateriales_MaterialId",
                table: "ProductoMateriales",
                column: "MaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertasStock");

            migrationBuilder.DropTable(
                name: "ComprobantesVenta");

            migrationBuilder.DropTable(
                name: "Despachos");

            migrationBuilder.DropTable(
                name: "DetalleOrdenMateriales");

            migrationBuilder.DropTable(
                name: "DetallesPedido");

            migrationBuilder.DropTable(
                name: "InspeccionesCalidad");

            migrationBuilder.DropTable(
                name: "MovimientosInventario");

            migrationBuilder.DropTable(
                name: "ProductoMateriales");

            migrationBuilder.DropTable(
                name: "Pedidos");

            migrationBuilder.DropTable(
                name: "OrdenesProduccion");

            migrationBuilder.DropTable(
                name: "Materiales");

            migrationBuilder.DropTable(
                name: "ProductosTerminados");

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}

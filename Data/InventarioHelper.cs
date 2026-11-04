using System;
using System.Linq;
using DIS_Monher.Models;

namespace DIS_Monher.Data
{
    public static class InventarioHelper
    {
        // Crea, actualiza o cierra la alerta según el stock real del material
        public static void EvaluarAlerta(ApplicationDbContext ctx, Material m)
        {
            var alerta = ctx.AlertasStock.FirstOrDefault(a => a.MaterialId == m.Id && !a.Atendida);

            if (m.StockActual <= m.StockMinimo)
            {
                var sugerida = Math.Max((m.StockMinimo * 2) - m.StockActual, 0);
                if (alerta == null)
                    ctx.AlertasStock.Add(new AlertaStock
                    {
                        MaterialId = m.Id,
                        CantidadActual = m.StockActual,
                        CantidadSugeridaCompra = sugerida
                    });
                else
                {
                    alerta.CantidadActual = m.StockActual;
                    alerta.CantidadSugeridaCompra = sugerida;
                }
            }
            else if (alerta != null)
            {
                alerta.Atendida = true; // el stock se recuperó
            }
        }
    }
}
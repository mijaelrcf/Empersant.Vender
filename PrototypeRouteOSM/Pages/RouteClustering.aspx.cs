using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Services;
using System.Web.Services;
using PrototypeRouteOSM.Models; // Asegurate de tener tu modelo CustomerLocation

namespace PrototypeRouteOSM.Pages
{
    public partial class RouteClustering : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e) { }

        // Clase para devolver los datos al mapa
        public class GrupoEntrega
        {
            public int DiaId { get; set; } // 1 = Lunes, 2 = Martes...
            public string NombreDia { get; set; }
            public string ColorHex { get; set; }
            public List<CustomerLocation> Clientes { get; set; }
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static List<GrupoEntrega> AgruparClientesPorDias(List<CustomerLocation> clientes, int cantidadDias)
        {
            if (clientes == null || clientes.Count == 0) return new List<GrupoEntrega>();

            // ALGORITMO K-MEANS
            // 1. Inicializar centroides aleatorios
            var centroides = InicializarCentroides(clientes, cantidadDias);
            var asignaciones = new int[clientes.Count];
            bool cambio = true;
            int iteraciones = 0;

            // 2. Iterar hasta converger (o max 100 veces)
            while (cambio && iteraciones < 100)
            {
                cambio = false;
                iteraciones++;

                // A. Asignar cada punto al centroide más cercano
                for (int i = 0; i < clientes.Count; i++)
                {
                    int mejorCentroide = -1;
                    double menorDistancia = double.MaxValue;

                    for (int k = 0; k < centroides.Count; k++)
                    {
                        double dist = DistanciaCuadrada(clientes[i], centroides[k]);
                        if (dist < menorDistancia)
                        {
                            menorDistancia = dist;
                            mejorCentroide = k;
                        }
                    }

                    if (asignaciones[i] != mejorCentroide)
                    {
                        asignaciones[i] = mejorCentroide;
                        cambio = true;
                    }
                }

                // B. Recalcular centroides (promedio de sus puntos)
                for (int k = 0; k < cantidadDias; k++)
                {
                    var puntosDelCluster = new List<CustomerLocation>();
                    for (int i = 0; i < clientes.Count; i++)
                    {
                        if (asignaciones[i] == k) puntosDelCluster.Add(clientes[i]);
                    }

                    if (puntosDelCluster.Count > 0)
                    {
                        centroides[k].Latitud = puntosDelCluster.Average(c => c.Latitud);
                        centroides[k].Longitud = puntosDelCluster.Average(c => c.Longitud);
                    }
                }
            }

            // 3. Preparar respuesta
            var resultado = new List<GrupoEntrega>();
            string[] colores = { "#e6194b", "#3cb44b", "#ffe119", "#4363d8", "#f58231", "#911eb4", "#46f0f0" };
            string[] dias = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };

            for (int k = 0; k < cantidadDias; k++)
            {
                var grupo = new GrupoEntrega
                {
                    DiaId = k + 1,
                    NombreDia = dias[k % 7] + " (Zona " + (k + 1) + ")",
                    ColorHex = colores[k % colores.Length],
                    Clientes = new List<CustomerLocation>()
                };

                for (int i = 0; i < clientes.Count; i++)
                {
                    if (asignaciones[i] == k)
                    {
                        // Asignamos el color al objeto cliente para usarlo en el front si es necesario
                        grupo.Clientes.Add(clientes[i]);
                    }
                }
                resultado.Add(grupo);
            }

            return resultado;
        }

        // Helpers Matemáticos
        private static List<CustomerLocation> InicializarCentroides(List<CustomerLocation> datos, int k)
        {
            var random = new Random();
            return datos.OrderBy(x => random.Next()).Take(k).Select(c => new CustomerLocation { Latitud = c.Latitud, Longitud = c.Longitud }).ToList();
        }

        private static double DistanciaCuadrada(CustomerLocation a, CustomerLocation b)
        {
            return Math.Pow(a.Latitud - b.Latitud, 2) + Math.Pow(a.Longitud - b.Longitud, 2);
        }
    }
}
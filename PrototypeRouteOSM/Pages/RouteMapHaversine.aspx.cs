using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Services;
using System.Web.Services;
using Google.OrTools.ConstraintSolver;
using PrototypeRouteOSM.Data;
using PrototypeRouteOSM.Models;

namespace PrototypeRouteOSM.Pages
{
    public partial class RouteMapHaversine : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e) { }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static List<RoutePlan> CalcularRutas(
            List<CustomerLocation> clientes,
            CustomerLocation deposito,
            int numVendedores
        )
        {
            // 1. Validaciones básicas
            if (clientes == null || clientes.Count == 0)
                return new List<RoutePlan>();

            // 2. Aquí llamamos al método que ORDENA las coordenadas
            // Para este prototipo usamos "Vecino Más Cercano".
            List<CustomerLocation> rutaOrdenada = OptimizarRutaVecinoMasCercano(clientes, deposito);

            // 3. ASIGNAR ORDEN DE RECORRIDO A CADA PUNTO
            for (int i = 0; i < rutaOrdenada.Count; i++)
                rutaOrdenada[i].OrdenRecorrido = i;

            // 4. Devolvemos la ruta empaquetada
            return new List<RoutePlan>
            {
                new RoutePlan
                {
                    VendedorId = 1, // Por ahora 1 solo vendedor
                    Puntos = rutaOrdenada
                }
            };
        }

        // --- MÉTODO DE ORDENAMIENTO (ALGORITMO) ---
        private static List<CustomerLocation> OptimizarRutaVecinoMasCercano(
            List<CustomerLocation> clientesDesordenados,
            CustomerLocation puntoPartida
        )
        {
            var ruta = new List<CustomerLocation>();
            var pendientes = new List<CustomerLocation>(clientesDesordenados);

            // Empezamos desde el depósito
            var puntoActual = puntoPartida;
            ruta.Add(puntoActual);

            while (pendientes.Count > 0)
            {
                // Buscamos cuál de los pendientes está más cerca del punto actual
                var masCercano = pendientes
                    .OrderBy(
                        c =>
                            DistanciaHaversine(
                                puntoActual.Latitud,
                                puntoActual.Longitud,
                                c.Latitud,
                                c.Longitud
                            )
                    )
                    .First();

                // Lo agregamos a la ruta y lo quitamos de pendientes
                ruta.Add(masCercano);
                pendientes.Remove(masCercano);

                // Ahora nuestro nuevo punto de partida es este cliente
                puntoActual = masCercano;
            }

            return ruta;
        }

        // Cálculo de distancia matemática (Fórmula de Haversine para mayor precisión que Pitágoras)
        private static double DistanciaHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radio de la tierra en km
            var dLat = A_Radianes(lat2 - lat1);
            var dLon = A_Radianes(lon2 - lon1);
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(A_Radianes(lat1))
                    * Math.Cos(A_Radianes(lat2))
                    * Math.Sin(dLon / 2)
                    * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double A_Radianes(double angulo)
        {
            return (Math.PI / 180) * angulo;
        }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static List<CustomerLocation> GetListCustomerLocation()
        {
            var repo = new CustomerRepository();
            return repo.GetAll();
        }
    }
}

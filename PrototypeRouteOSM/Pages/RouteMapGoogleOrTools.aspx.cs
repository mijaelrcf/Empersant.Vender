using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using Google.OrTools.ConstraintSolver;
using PrototypeRouteOSM.Data;
using PrototypeRouteOSM.Models;

namespace PrototypeRouteOSM.Pages
{
    public partial class RouteMapGoogleOrTools : System.Web.UI.Page
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
            // Test Library GoogleOrTools
            // Verificar que el servidor esté corriendo en 64 bits,
            // porque Google OR-Tools no funciona en 32 bits.
            if (IntPtr.Size == 4)
            {
                throw new Exception(
                    "ERROR: El servidor sigue corriendo en 32 bits (x86). Google OR-Tools requiere 64 bits."
                );
            }

            // 1. Validaciones básicas
            if (clientes == null || clientes.Count == 0)
                return new List<RoutePlan>();

            // 2. Aquí llamamos al método que ORDENA las coordenadas
            // Usamos el motor profesional de Google en lugar del manual
            List<CustomerLocation> rutaOrdenada = OptimizarConGoogleOrTools(
                clientes,
                deposito,
                numVendedores
            );

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

        private static List<CustomerLocation> OptimizarConGoogleOrTools(
            List<CustomerLocation> clientes,
            CustomerLocation deposito,
            int numVehiculos
        )
        {
            // 1. Preparamos la lista completa (Depósito es el índice 0)
            var todosLosPuntos = new List<CustomerLocation> { deposito };
            todosLosPuntos.AddRange(clientes);

            // 2. Crear la Matriz de Distancias (Todos contra todos)
            // Google OR-Tools necesita saber la distancia de A a B, de A a C, de B a C, etc.
            int count = todosLosPuntos.Count;
            long[,] matrizDistancias = new long[count, count];

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    if (i == j)
                        matrizDistancias[i, j] = 0;
                    else
                    {
                        // Calculamos distancia en Metros para tener precisión entera
                        double distKm = DistanciaHaversine(
                            todosLosPuntos[i].Latitud,
                            todosLosPuntos[i].Longitud,
                            todosLosPuntos[j].Latitud,
                            todosLosPuntos[j].Longitud
                        );
                        matrizDistancias[i, j] = (long)(distKm * 1000);
                    }
                }
            }

            // 3. Configurar el Gestor de Rutas (Routing Index Manager)
            // Argumentos: Número de lugares, Número de vehículos, Índice del depósito (0)
            RoutingIndexManager manager = new RoutingIndexManager(count, numVehiculos, 0);

            // 4. Configurar el Modelo de Rutas
            RoutingModel routing = new RoutingModel(manager);

            // 5. Definir la función de costo (Callback de distancia)
            int transitCallbackIndex = routing.RegisterTransitCallback(
                (long fromIndex, long toIndex) =>
                {
                    var fromNode = manager.IndexToNode(fromIndex);
                    var toNode = manager.IndexToNode(toIndex);
                    return matrizDistancias[fromNode, toNode];
                }
            );

            // Definir que el costo del viaje es la distancia
            routing.SetArcCostEvaluatorOfAllVehicles(transitCallbackIndex);

            // 6. Parámetros de búsqueda (Heurística automática)
            RoutingSearchParameters searchParameters =
                operations_research_constraint_solver.DefaultRoutingSearchParameters();

            // "PathCheapestArc" es rápido y muy bueno para iniciar
            searchParameters.FirstSolutionStrategy = FirstSolutionStrategy
                .Types
                .Value
                .PathCheapestArc;

            // 7. ¡RESOLVER!
            Assignment solution = routing.SolveWithParameters(searchParameters);

            // 8. Convertir la solución de Google a nuestra lista de Clientes
            var rutaResultado = new List<CustomerLocation>();

            if (solution != null)
            {
                // Obtenemos la ruta del vehículo 0 (porque solo mandamos 1 por ahora)
                long index = routing.Start(0);

                while (!routing.IsEnd(index))
                {
                    int nodoReal = manager.IndexToNode(index);
                    rutaResultado.Add(todosLosPuntos[nodoReal]);

                    // Siguiente paso
                    index = solution.Value(routing.NextVar(index));
                }

                // (Opcional) Agregar el depósito al final si quieres ruta circular
                // rutaResultado.Add(deposito);
            }
            else
            {
                // Si falla, devolvemos la lista tal cual (fallback)
                return todosLosPuntos;
            }

            return rutaResultado;
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

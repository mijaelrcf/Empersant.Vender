using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Services;
using System.Web.Services;
using Google.OrTools.ConstraintSolver;
using PrototypeRouteOSM.Models;

namespace PrototypeRouteOSM.Pages
{
    public partial class RouteMap2 : System.Web.UI.Page
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
            // Test Library OrTools: Verificar que el servidor esté corriendo en 64 bits, porque Google OR-Tools no funciona en 32 bits.
            if (IntPtr.Size == 4)
            {
                throw new Exception("ERROR: El servidor sigue corriendo en 32 bits (x86). Google OR-Tools requiere 64 bits.");
            }

            // 1. Validaciones básicas
            if (clientes == null || clientes.Count == 0)
                return new List<RoutePlan>();

            // Opcion 1. Metodo Manual
            // 2. Aquí llamamos al método que ORDENA las coordenadas
            // Para este prototipo usamos "Vecino Más Cercano".
            // List<CustomerLocation> rutaOrdenada = OptimizarRutaVecinoMasCercano(clientes, deposito);

            // Opcion 2. Metodo usando herramienta de Google
            // Usamos el motor profesional de Google en lugar del manual
            List<CustomerLocation> rutaOrdenada = OptimizarConGoogleOrTools(
                clientes,
                deposito,
                numVendedores
            );

            // 3. Devolvemos la ruta empaquetada
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
                            todosLosPuntos[i].Latitud, todosLosPuntos[i].Longitud,
                            todosLosPuntos[j].Latitud, todosLosPuntos[j].Longitud
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
            return new List<CustomerLocation>
            {
                new CustomerLocation
                {
                    ClienteId = 861,
                    NombreCompleto = "IRIS RAMOS",
                    Latitud = -16.47537,
                    Longitud = -68.149709
                },
                new CustomerLocation
                {
                    ClienteId = 877,
                    NombreCompleto = "FRANCISCO CALCINA MAMANI",
                    Latitud = -16.47469386,
                    Longitud = -68.15000352
                },
                new CustomerLocation
                {
                    ClienteId = 1013,
                    NombreCompleto = "Nora Chavez Aruquipa",
                    Latitud = -16.44926514,
                    Longitud = -68.15048765
                },
                new CustomerLocation
                {
                    ClienteId = 1023,
                    NombreCompleto = "Maria Paula Zalles Jove",
                    Latitud = -16.47240179,
                    Longitud = -68.15372676
                },
                new CustomerLocation
                {
                    ClienteId = 1024,
                    NombreCompleto = "Ruth Silvia Gorostiaga Calani",
                    Latitud = -16.47277507,
                    Longitud = -68.15330699
                },
                new CustomerLocation
                {
                    ClienteId = 1026,
                    NombreCompleto = "Patricia Pascuala Quispe de Gutiérrez",
                    Latitud = -16.47395631,
                    Longitud = -68.15351084
                },
                new CustomerLocation
                {
                    ClienteId = 1154,
                    NombreCompleto = "ANGÉLICA CHOQUE",
                    Latitud = -16.48103749,
                    Longitud = -68.14894035
                },
                new CustomerLocation
                {
                    ClienteId = 1997,
                    NombreCompleto = "Paulina Ulo Pacosillo",
                    Latitud = -16.46694849,
                    Longitud = -68.15437552
                },
                new CustomerLocation
                {
                    ClienteId = 2174,
                    NombreCompleto = "JAQUELINE CONDORI",
                    Latitud = -16.4747678,
                    Longitud = -68.15010343
                },
                new CustomerLocation
                {
                    ClienteId = 2194,
                    NombreCompleto = "PAULINA URQUIZO UNIV",
                    Latitud = -16.46791371,
                    Longitud = -68.15063752
                },
                new CustomerLocation
                {
                    ClienteId = 2439,
                    NombreCompleto = "Angelica Ramos",
                    Latitud = -16.47033926,
                    Longitud = -68.15102141
                },
                new CustomerLocation
                {
                    ClienteId = 2576,
                    NombreCompleto = "SONIA JIMENEZ",
                    Latitud = -16.44644156,
                    Longitud = -68.1517265
                },
                new CustomerLocation
                {
                    ClienteId = 2724,
                    NombreCompleto = "MARISOL POMA QUISPE",
                    Latitud = -16.46741792,
                    Longitud = -68.14874925
                },
                new CustomerLocation
                {
                    ClienteId = 2972,
                    NombreCompleto = "Susi QUISPE COPA",
                    Latitud = -16.45151182,
                    Longitud = -68.14932525
                },
                new CustomerLocation
                {
                    ClienteId = 3067,
                    NombreCompleto = "TERESA LIMACHI UNIV",
                    Latitud = -16.46784522,
                    Longitud = -68.15063953
                },
                new CustomerLocation
                {
                    ClienteId = 3270,
                    NombreCompleto = "SILVIA HUANCA ORTIZ",
                    Latitud = -16.45976615,
                    Longitud = -68.14977117
                },
                new CustomerLocation
                {
                    ClienteId = 3293,
                    NombreCompleto = "Juana Ramos",
                    Latitud = -16.449216,
                    Longitud = -68.150162
                },
                new CustomerLocation
                {
                    ClienteId = 3334,
                    NombreCompleto = "Carla alvares",
                    Latitud = -16.45729063,
                    Longitud = -68.14942416
                },
                new CustomerLocation
                {
                    ClienteId = 3798,
                    NombreCompleto = "MARINA POMA UNV",
                    Latitud = -16.462265,
                    Longitud = -68.152003
                },
                new CustomerLocation
                {
                    ClienteId = 4192,
                    NombreCompleto = "KAREN AGUILAR JIMENEZ",
                    Latitud = -16.467208,
                    Longitud = -68.149674
                },
                //new CustomerLocation
                //{
                //    ClienteId = 4196,
                //    NombreCompleto = "CAROLA JIMENEZ UNV",
                //    Latitud = -16.47475462,
                //    Longitud = -68.14997938
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 4226,
                //    NombreCompleto = "Maruja Villegas",
                //    Latitud = -16.465157,
                //    Longitud = -68.149051
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 4228,
                //    NombreCompleto = "PAMELA PATTY UNV",
                //    Latitud = -16.47913227,
                //    Longitud = -68.14861849
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 4229,
                //    NombreCompleto = "RICHAR TICONA UNV",
                //    Latitud = -16.4753,
                //    Longitud = -68.149756
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 4230,
                //    NombreCompleto = "VERONICA QUENALLATA UNV",
                //    Latitud = -16.475783,
                //    Longitud = -68.149628
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 4231,
                //    NombreCompleto = "JAIME ALVAREZ UNV",
                //    Latitud = -16.47583,
                //    Longitud = -68.149513
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 4382,
                //    NombreCompleto = "ALICE ZAPATA",
                //    Latitud = -16.47483275,
                //    Longitud = -68.14994819
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 4485,
                //    NombreCompleto = "TATIANA RIOS UNV.",
                //    Latitud = -16.471673,
                //    Longitud = -68.150678
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 4534,
                //    NombreCompleto = "ROXANA GUARI",
                //    Latitud = -16.466444,
                //    Longitud = -68.151283
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 4786,
                //    NombreCompleto = "VERONICA VALLEJOS",
                //    Latitud = -16.468614,
                //    Longitud = -68.150812
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 6590,
                //    NombreCompleto = "ISABEL RENGEL",
                //    Latitud = -16.467506,
                //    Longitud = -68.149162
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 6597,
                //    NombreCompleto = "ROXANA LIMACHI",
                //    Latitud = -16.471876,
                //    Longitud = -68.150563
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 6685,
                //    NombreCompleto = "Inocencia Andia Flores",
                //    Latitud = -16.474975,
                //    Longitud = -68.149353
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 6835,
                //    NombreCompleto = "MARIA PAOLA APAZA",
                //    Latitud = -16.445777,
                //    Longitud = -68.152038
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 6861,
                //    NombreCompleto = "LYCET RAMOS",
                //    Latitud = -16.480304,
                //    Longitud = -68.151438
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 7021,
                //    NombreCompleto = "VALERIA CHOQUEHUANCA",
                //    Latitud = -16.467882,
                //    Longitud = -68.150674
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 7089,
                //    NombreCompleto = "Carla Faviola Llave Yugar",
                //    Latitud = -16.488148,
                //    Longitud = -68.140357
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 7137,
                //    NombreCompleto = "Limpieza Cecilab",
                //    Latitud = -16.492453,
                //    Longitud = -68.134651
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 7185,
                //    NombreCompleto = "Gretzel Aranda Garcia",
                //    Latitud = -16.460945,
                //    Longitud = -68.150507
                //},
                //new CustomerLocation
                //{
                //    ClienteId = 7201,
                //    NombreCompleto = "Patricia Flores",
                //    Latitud = -16.469938,
                //    Longitud = -68.150838
                //}
            };
        }
    }
}

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
    public partial class RouteMapHaversine2_Opt : System.Web.UI.Page
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

            // 2. ALGORITMO HÍBRIDO (Vecino Cercano + 2-Opt)
            // Es mucho mejor que el Vecino Cercano solo.
            List<CustomerLocation> rutaOrdenada = OptimizarHibrido(clientes, deposito);

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

        // ==========================================================
        //       ALGORITMO HÍBRIDO: VECINO MÁS CERCANO + 2-OPT
        // ==========================================================
        private static List<CustomerLocation> OptimizarHibrido(List<CustomerLocation> clientes, CustomerLocation deposito)
        {
            // PASO 1: Crear una solución inicial rápida usando Vecino Más Cercano
            // Esto nos da un "borrador" de la ruta.
            List<CustomerLocation> ruta = GenerarRutaInicialVecino(clientes, deposito);

            // PASO 2: Mejorar la solución usando 2-Opt
            // Esto desenreda los cruces y optimiza el recorrido.
            ruta = MejorarCon2Opt(ruta);

            return ruta;
        }

        // --- FASE 1: Vecino Más Cercano (Tu código original mejorado) ---
        private static List<CustomerLocation> GenerarRutaInicialVecino(List<CustomerLocation> clientes, CustomerLocation deposito)
        {
            var ruta = new List<CustomerLocation>();
            var pendientes = new List<CustomerLocation>(clientes);

            var puntoActual = deposito;
            ruta.Add(puntoActual); // El depósito es fijo al inicio

            while (pendientes.Count > 0)
            {
                // Buscamos el más cercano de la lista de pendientes
                var masCercano = pendientes
                    .OrderBy(c => DistanciaHaversine(puntoActual.Latitud, puntoActual.Longitud, c.Latitud, c.Longitud))
                    .First();

                ruta.Add(masCercano);
                pendientes.Remove(masCercano);
                puntoActual = masCercano;
            }
            return ruta;
        }

        // --- FASE 2: Algoritmo 2-Opt ---
        private static List<CustomerLocation> MejorarCon2Opt(List<CustomerLocation> rutaInicial)
        {
            List<CustomerLocation> mejorRuta = new List<CustomerLocation>(rutaInicial);
            bool mejoraEncontrada = true;
            int maxIteraciones = 0; // Para evitar bucles infinitos en casos raros

            // Repetimos el proceso mientras sigamos encontrando mejoras
            while (mejoraEncontrada && maxIteraciones < 100)
            {
                mejoraEncontrada = false;
                double mejorDistancia = CalcularDistanciaTotal(mejorRuta);

                // Recorremos la ruta buscando dos conexiones que se crucen
                // Empezamos en 1 para NO mover el Depósito (índice 0)
                for (int i = 1; i < mejorRuta.Count - 2; i++)
                {
                    for (int j = i + 1; j < mejorRuta.Count - 1; j++)
                    {
                        // Si intercambiamos los caminos, ¿es más corto?
                        // Intenta conectar (i-1) con (j) en lugar de (i-1) con (i)

                        List<CustomerLocation> nuevaRuta = HacerIntercambio2Opt(mejorRuta, i, j);
                        double nuevaDistancia = CalcularDistanciaTotal(nuevaRuta);

                        // Si encontramos una distancia menor, nos quedamos con el cambio
                        // Usamos un pequeño margen de error (0.001) para flotantes
                        if (nuevaDistancia < mejorDistancia - 0.001)
                        {
                            mejorRuta = nuevaRuta;
                            mejorDistancia = nuevaDistancia;
                            mejoraEncontrada = true;
                            // Reiniciamos el bucle para buscar más mejoras en la nueva ruta
                            break;
                        }
                    }
                    if (mejoraEncontrada) break;
                }
                maxIteraciones++;
            }

            return mejorRuta;
        }

        // Helper para invertir un segmento de la ruta (La magia del 2-Opt)
        private static List<CustomerLocation> HacerIntercambio2Opt(List<CustomerLocation> ruta, int i, int j)
        {
            var nuevaRuta = new List<CustomerLocation>();

            // 1. Agregar ruta desde el inicio hasta el punto antes del corte (0 a i-1)
            for (int k = 0; k < i; k++) nuevaRuta.Add(ruta[k]);

            // 2. Agregar el segmento invertido (de j bajando hasta i)
            for (int k = j; k >= i; k--) nuevaRuta.Add(ruta[k]);

            // 3. Agregar el resto de la ruta (j+1 hasta el final)
            for (int k = j + 1; k < ruta.Count; k++) nuevaRuta.Add(ruta[k]);

            return nuevaRuta;
        }

        // Helper para calcular la distancia de TODA la ruta
        private static double CalcularDistanciaTotal(List<CustomerLocation> ruta)
        {
            double total = 0;
            for (int i = 0; i < ruta.Count - 1; i++)
            {
                total += DistanciaHaversine(
                    ruta[i].Latitud, ruta[i].Longitud,
                    ruta[i + 1].Latitud, ruta[i + 1].Longitud);
            }
            return total;
        }

        // --- MATEMÁTICAS ---
        private static double DistanciaHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Radio tierra km
            var dLat = A_Radianes(lat2 - lat1);
            var dLon = A_Radianes(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(A_Radianes(lat1)) * Math.Cos(A_Radianes(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
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

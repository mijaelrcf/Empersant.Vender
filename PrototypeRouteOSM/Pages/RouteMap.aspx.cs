using System;
using System.Collections.Generic;
using System.Web.Script.Services;
using System.Web.Services;

namespace PrototypeRouteOSM.Pages
{
    public partial class RouteMap : System.Web.UI.Page
    {
        // Estructura simple para coordenadas
        public class Ubicacion
        {
            public string Nombre { get; set; }
            public double Lat { get; set; }
            public double Lng { get; set; }
        }

        public class ResultadoRuta
        {
            public int VendedorId { get; set; }
            public List<Ubicacion> Puntos { get; set; } // Ordenados
        }

        protected void Page_Load(object sender, EventArgs e) { }

        [WebMethod]
        [ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public static List<ResultadoRuta> CalcularRutas(
            List<Ubicacion> clientes,
            Ubicacion deposito,
            int numVendedores
        )
        {
            // AQUI IRIA LA LOGICA COMPLEJA (Google OR-Tools).
            // Para este prototipo, haremos una simulación simplificada:
            // 1. Dividir clientes entre vendedores.
            // 2. Ordenar por cercanía simple (Vecino más cercano).

            List<ResultadoRuta> resultado = new List<ResultadoRuta>();
            var clientesPendientes = new List<Ubicacion>(clientes);

            // Simulación simple de reparto (Round Robin para el ejemplo)
            for (int i = 0; i < numVendedores; i++)
            {
                resultado.Add(
                    new ResultadoRuta
                    {
                        VendedorId = i + 1,
                        Puntos = new List<Ubicacion> { deposito } // Todos empiezan en el depósito
                    }
                );
            }

            int vendedorActual = 0;
            while (clientesPendientes.Count > 0)
            {
                // En un caso real, aquí calculas la matriz de distancia
                // Tomamos el primer cliente y se lo asignamos al vendedor actual
                var cliente = clientesPendientes[0];

                resultado[vendedorActual].Puntos.Add(cliente);
                clientesPendientes.RemoveAt(0);

                // Pasar al siguiente vendedor
                vendedorActual = (vendedorActual + 1) % numVendedores;
            }

            // Opcional: Agregar retorno al depósito al final
            foreach (var ruta in resultado)
            {
                ruta.Puntos.Add(deposito);
            }

            return resultado;
        }
    }
}

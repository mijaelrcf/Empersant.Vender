using Detesim.Vender.ClienteOrigenGis;
using Detesim.Vender.ZonaGisOrigen;
using log4net;
using PrototypeRouteOSM.Data;
using PrototypeRouteOSM.Models;
using PrototypeRouteOSM.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;

// Asumiendo que tienes estas clases en tu proyecto
// Si no las tienes, te las proporciono más abajo
//using Detesim.Vender.ClienteOrigenGis;
//using Detesim.Vender.ZonaGisOrigen;
//using Detesim.Vender.Utilities.AngularUtility;

namespace PrototypeRouteOSM.Pages
{
    public partial class RouteMapAngularSweep : System.Web.UI.Page
    {
        private static readonly ILog log = LogManager.GetLogger("Standard");

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!User.Identity.IsAuthenticated)
                return;

            if (!IsPostBack)
            {
                // Inicializar con valores por defecto
                NroDiasTB.Text = "5";
            }
        }

        /// <summary>
        /// Botón para cargar datos DEMO de 100 clientes en La Paz
        /// </summary>
        protected void CargarDemoButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Generar 100 clientes de ejemplo en La Paz
                List<ClienteOrigenGis> clientesDemo = GetListCustomerLocation();

                // Número de zonas por defecto
                int nroZonas = 5;
                try
                {
                    nroZonas = Convert.ToInt32(NroDiasTB.Text);
                    if (nroZonas < 1 || nroZonas > 7)
                        nroZonas = 5;
                }
                catch
                {
                    nroZonas = 5;
                }

                // Generar zonas usando el algoritmo
                List<ZonaGisOrigen> zonas = AngularSweepUtility.GeneradorRutasSweepTabu.GenerarRutas(
                    clientesDemo,
                    nroZonas,
                    tabuIteraciones: 2000,
                    tabuTenure: 40
                );

                if (zonas != null && zonas.Count > 0)
                {
                    // Guardar en Session para persistencia
                    Session["ZonasGeneradas"] = zonas;

                    MostrarZonasEnMapa(zonas);
                    MostrarEstadisticasGlobales(zonas);

                    log.Info($"Demo cargado: {clientesDemo.Count} clientes distribuidos en {zonas.Count} zonas");
                }
                else
                {
                    MostrarMensajeError("No se pudieron generar las zonas. Intente nuevamente.");
                }
            }
            catch (Exception ex)
            {
                log.Error("Error al cargar demo", ex);
                MostrarMensajeError("Error al cargar datos de demostración: " + ex.Message);
            }
        }

        /// <summary>
        /// Botón para generar zonas con datos reales (deberás conectarlo a tu BD)
        /// </summary>
        protected void GenerarZonasButton_Click(object sender, EventArgs e)
        {
            int nroDias = 0;

            try
            {
                nroDias = Convert.ToInt32(NroDiasTB.Text);
            }
            catch { }

            if (nroDias < 1 || nroDias > 7)
            {
                MostrarMensajeError("Debe ingresar un número entre 1 y 7");
                return;
            }

            // AQUÍ DEBERÍAS OBTENER LOS CLIENTES DE TU BASE DE DATOS
            // Por ahora, usamos el demo
            List<ClienteOrigenGis> theList = GetListCustomerLocation();

            if (theList == null || theList.Count == 0)
            {
                MostrarMensajeError("La lista de clientes está vacía");
                return;
            }

            // Generar zonas usando el algoritmo Angular Sweep + Tabu Search
            List<ZonaGisOrigen> zonas = AngularSweepUtility.GeneradorRutasSweepTabu.GenerarRutas(
                theList,
                nroDias,
                tabuIteraciones: 2000,
                tabuTenure: 40
            );

            if (zonas != null && zonas.Count > 0)
            {
                Session["ZonasGeneradas"] = zonas;
                MostrarZonasEnMapa(zonas);
                MostrarEstadisticasGlobales(zonas);
            }
            else
            {
                MostrarMensajeError("No se pudieron generar las zonas");
            }
        }

        /// <summary>
        /// Muestra las zonas generadas en el mapa
        /// </summary>
        private void MostrarZonasEnMapa(List<ZonaGisOrigen> zonas)
        {
            if (zonas == null || zonas.Count == 0)
            {
                MostrarMensajeError("No hay zonas para mostrar");
                GuardarLB.Visible = false;
                ZonasPanel.Visible = false;
                return;
            }

            GuardarLB.Visible = true;
            ZonasPanel.Visible = true;

            int maxZonaId = zonas.Max(z => z.ZonaId);

            // Mostrar/ocultar paneles según el número de zonas
            Panel1.Visible = maxZonaId >= 1;
            Panel2.Visible = maxZonaId >= 2;
            Panel3.Visible = maxZonaId >= 3;
            Panel4.Visible = maxZonaId >= 4;
            Panel5.Visible = maxZonaId >= 5;
            Panel6.Visible = maxZonaId >= 6;
            Panel7.Visible = maxZonaId >= 7;

            // Llenar datos de cada zona
            foreach (var zona in zonas)
            {
                int nroClientes = zona.Clientes.Count;
                decimal montoTotal = zona.Clientes.Sum(c => c.PromedioVentas);
                int tiempoTotal = zona.Clientes.Sum(c => c.TiempoPdv);

                // Calcular distancia total de la ruta
                double distanciaKm = CalcularDistanciaRuta(zona.Clientes);

                // Estimar tiempo de viaje (asumiendo 30 km/h promedio en ciudad)
                int tiempoViajeMin = (int)(distanciaKm / 30.0 * 60);
                int tiempoTotalConViaje = tiempoTotal + tiempoViajeMin;

                switch (zona.ZonaId)
                {
                    case 1:
                        Clientes1Label.Text = nroClientes.ToString();
                        Monto1Label.Text = montoTotal.ToString("C");
                        Tiempo1Label.Text = $"{distanciaKm:F2} km | {tiempoTotalConViaje} min total";
                        break;
                    case 2:
                        Clientes2Label.Text = nroClientes.ToString();
                        Monto2Label.Text = montoTotal.ToString("C");
                        Tiempo2Label.Text = $"{distanciaKm:F2} km | {tiempoTotalConViaje} min total";
                        break;
                    case 3:
                        Clientes3Label.Text = nroClientes.ToString();
                        Monto3Label.Text = montoTotal.ToString("C");
                        Tiempo3Label.Text = $"{distanciaKm:F2} km | {tiempoTotalConViaje} min total";
                        break;
                    case 4:
                        Clientes4Label.Text = nroClientes.ToString();
                        Monto4Label.Text = montoTotal.ToString("C");
                        Tiempo4Label.Text = $"{distanciaKm:F2} km | {tiempoTotalConViaje} min total";
                        break;
                    case 5:
                        Clientes5Label.Text = nroClientes.ToString();
                        Monto5Label.Text = montoTotal.ToString("C");
                        Tiempo5Label.Text = $"{distanciaKm:F2} km | {tiempoTotalConViaje} min total";
                        break;
                    case 6:
                        Clientes6Label.Text = nroClientes.ToString();
                        Monto6Label.Text = montoTotal.ToString("C");
                        Tiempo6Label.Text = $"{distanciaKm:F2} km | {tiempoTotalConViaje} min total";
                        break;
                    case 7:
                        Clientes7Label.Text = nroClientes.ToString();
                        Monto7Label.Text = montoTotal.ToString("C");
                        Tiempo7Label.Text = $"{distanciaKm:F2} km | {tiempoTotalConViaje} min total";
                        break;
                }
            }

            // Generar JSON para el mapa
            GenerarJSONParaMapa(zonas);
        }

        /// <summary>
        /// Genera el JSON que se enviará al JavaScript para renderizar el mapa
        /// </summary>
        private void GenerarJSONParaMapa(List<ZonaGisOrigen> zonas)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var zona in zonas)
            {
                foreach (var cliente in zona.Clientes)
                {
                    if (sb.Length > 0)
                        sb.Append(",");

                    sb.Append("{");
                    sb.AppendFormat("\"ZonaId\":{0},", zona.ZonaId);
                    sb.AppendFormat("\"ClienteId\":{0},", cliente.ClienteId);
                    sb.AppendFormat("\"Nombre\":\"{0}\",", EscapeJSON(cliente.Nombre));
                    sb.AppendFormat("\"Latitud\":{0},", cliente.Latitud.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.AppendFormat("\"Longitud\":{0},", cliente.Longitud.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.AppendFormat("\"PromedioVentas\":{0},", cliente.PromedioVentas.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    sb.AppendFormat("\"TiempoPdv\":{0}", cliente.TiempoPdv);
                    sb.Append("}");
                }
            }

            MapHF.Value = "[" + sb.ToString() + "]";

            // Registrar el script para inicializar el mapa
            ScriptManager.RegisterStartupScript(this, GetType(), "InitMap", "setTimeout(function(){ initMap(); }, 500);", true);
        }

        /// <summary>
        /// Muestra estadísticas globales del análisis
        /// </summary>
        private void MostrarEstadisticasGlobales(List<ZonaGisOrigen> zonas)
        {
            EstadisticasPanel.Visible = true;

            int totalClientes = zonas.Sum(z => z.Clientes.Count);
            decimal totalVentas = zonas.Sum(z => z.Clientes.Sum(c => c.PromedioVentas));
            int totalZonas = zonas.Count;
            decimal promedioClientesPorZona = totalClientes / (decimal)totalZonas;

            TotalClientesLabel.Text = totalClientes.ToString();
            TotalZonasLabel.Text = totalZonas.ToString();
            TotalVentasLabel.Text = totalVentas.ToString("C");
            PromedioClientesLabel.Text = promedioClientesPorZona.ToString("F1");
        }

        /// <summary>
        /// Guarda las zonas en la base de datos
        /// </summary>
        protected void GuardarLB_Click(object sender, EventArgs e)
        {
            var zonas = Session["ZonasGeneradas"] as List<ZonaGisOrigen>;

            if (zonas == null || zonas.Count == 0)
            {
                MostrarMensajeError("No hay zonas para guardar");
                return;
            }

            try
            {
                // AQUÍ DEBERÍAS IMPLEMENTAR LA LÓGICA DE GUARDADO EN TU BASE DE DATOS
                // Similar al código original GuardarLB_Click pero más limpio:

                string usuario = User.Identity.Name;

                // Ejemplo de guardado (adaptar a tu BLL):
                /*
                ZonaGisAngularBLL.LimpiarZonasGis(usuario);

                for (int i = 0; i < zonas.Count; i++)
                {
                    var zona = zonas[i];
                    string nombre = ObtenerNombreZona(zona.ZonaId);
                    int diaId = ObtenerDiaZona(zona.ZonaId);
                    decimal monto = zona.Clientes.Sum(c => c.PromedioVentas);

                    if (!string.IsNullOrEmpty(nombre) && diaId > 0)
                    {
                        ZonaGisAngularBLL.InsertarZonaGis(zona.ZonaId, nombre, diaId, monto, usuario);

                        foreach (var cliente in zona.Clientes)
                        {
                            ZonaClienteGisAngularBLL.InsertarZonaClienteGis(
                                zona.ZonaId, 
                                cliente.ClienteId, 
                                cliente.Nombre, 
                                cliente.Latitud, 
                                cliente.Longitud, 
                                cliente.PromedioVentas, 
                                cliente.TiempoPdv, 
                                usuario
                            );
                        }
                    }
                }
                */

                MostrarMensajeExito("Zonas guardadas exitosamente");
                log.Info($"Zonas guardadas por usuario: {usuario}");

                // Redirigir o limpiar
                // Response.Redirect("OtraPagina.aspx");
            }
            catch (Exception ex)
            {
                log.Error("Error al guardar zonas", ex);
                MostrarMensajeError("Error al guardar: " + ex.Message);
            }
        }

        #region Métodos Auxiliares

        private string ObtenerNombreZona(int zonaId)
        {
            switch (zonaId)
            {
                case 1: return Nombre1TB.Text;
                case 2: return Nombre2TB.Text;
                case 3: return Nombre3TB.Text;
                case 4: return Nombre4TB.Text;
                case 5: return Nombre5TB.Text;
                case 6: return Nombre6TB.Text;
                case 7: return Nombre7TB.Text;
                default: return "";
            }
        }

        private int ObtenerDiaZona(int zonaId)
        {
            try
            {
                switch (zonaId)
                {
                    case 1: return Convert.ToInt32(Dia1DDL.SelectedValue);
                    case 2: return Convert.ToInt32(Dia2DDL.SelectedValue);
                    case 3: return Convert.ToInt32(Dia3DDL.SelectedValue);
                    case 4: return Convert.ToInt32(Dia4DDL.SelectedValue);
                    case 5: return Convert.ToInt32(Dia5DDL.SelectedValue);
                    case 6: return Convert.ToInt32(Dia6DDL.SelectedValue);
                    case 7: return Convert.ToInt32(Dia7DDL.SelectedValue);
                    default: return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        private void MostrarMensajeError(string mensaje)
        {
            // Implementar según tu sistema de mensajes
            // Ejemplo: Mensajes.DisplaySystemErrorMessage(mensaje);
            ScriptManager.RegisterStartupScript(this, GetType(), "Error",
                $"alert('Error: {EscapeJS(mensaje)}');", true);
        }

        private void MostrarMensajeExito(string mensaje)
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "Exito",
                $"alert('Éxito: {EscapeJS(mensaje)}');", true);
        }

        private string EscapeJSON(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text.Replace("\\", "\\\\")
                       .Replace("\"", "\\\"")
                       .Replace("\r", "\\r")
                       .Replace("\n", "\\n")
                       .Replace("\t", "\\t");
        }

        private string EscapeJS(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            return text.Replace("'", "\\'")
                       .Replace("\"", "\\\"")
                       .Replace("\r", "")
                       .Replace("\n", "\\n");
        }

        #endregion

        #region Cálculo de Distancias de Ruta

        /// <summary>
        /// Calcula la distancia total de una ruta siguiendo el orden de la lista de clientes
        /// </summary>
        private double CalcularDistanciaRuta(List<ClienteOrigenGis> clientes)
        {
            if (clientes == null || clientes.Count < 2)
                return 0;

            double distanciaTotal = 0;

            // Sumar distancias entre cada par de clientes consecutivos
            for (int i = 0; i < clientes.Count - 1; i++)
            {
                distanciaTotal += CalcularDistanciaHaversine(
                    clientes[i].Latitud,
                    clientes[i].Longitud,
                    clientes[i + 1].Latitud,
                    clientes[i + 1].Longitud
                );
            }

            return distanciaTotal;
        }

        /// <summary>
        /// Calcula la distancia en kilómetros entre dos puntos usando la fórmula de Haversine
        /// </summary>
        private double CalcularDistanciaHaversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Radio de la Tierra en kilómetros

            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c; // Distancia en kilómetros
        }

        /// <summary>
        /// Convierte grados a radianes
        /// </summary>
        private double ToRadians(double grados)
        {
            return grados * Math.PI / 180.0;
        }

        #endregion

        #region Generador de Datos DEMO

        /// <summary>
        /// Genera 100 clientes de ejemplo distribuidos por La Paz, Bolivia
        /// </summary>
        private List<ClienteOrigenGis> GenerarClientesLaPazDemo()
        {
            var clientes = new List<ClienteOrigenGis>();
            Random rand = new Random(42); // Seed fijo para resultados reproducibles

            // Coordenadas de La Paz: Aproximadamente entre
            // Latitud: -16.45 a -16.55
            // Longitud: -68.08 a -68.20

            double latMin = -16.55;
            double latMax = -16.45;
            double lonMin = -68.20;
            double lonMax = -68.08;

            // Zonas importantes de La Paz para generar clusters realistas
            var zonasLaPaz = new[]
            {
            new { Nombre = "Centro", Lat = -16.4955, Lon = -68.1336, Peso = 0.25 },
            new { Nombre = "Zona Sur", Lat = -16.5290, Lon = -68.0739, Peso = 0.20 },
            new { Nombre = "El Alto", Lat = -16.5050, Lon = -68.1630, Peso = 0.15 },
            new { Nombre = "Sopocachi", Lat = -16.5070, Lon = -68.1200, Peso = 0.15 },
            new { Nombre = "Miraflores", Lat = -16.5150, Lon = -68.1100, Peso = 0.10 },
            new { Nombre = "Calacoto", Lat = -16.5300, Lon = -68.0650, Peso = 0.10 },
            new { Nombre = "San Miguel", Lat = -16.5200, Lon = -68.0950, Peso = 0.05 }
        };

            string[] tiposNegocio = { "Supermercado", "Minimarket", "Farmacia", "Restaurante", "Ferretería", "Librería", "Panadería" };
            string[] calles = { "Av. 6 de Agosto", "Av. Arce", "Prado", "Av. Camacho", "Av. Mariscal Santa Cruz", "Calle Comercio", "Av. Heroínas" };

            for (int i = 1; i <= 100; i++)
            {
                // Seleccionar una zona con probabilidad ponderada
                double r = rand.NextDouble();
                double acum = 0;
                var zonaSeleccionada = zonasLaPaz[0];

                foreach (var zona in zonasLaPaz)
                {
                    acum += zona.Peso;
                    if (r <= acum)
                    {
                        zonaSeleccionada = zona;
                        break;
                    }
                }

                // Generar coordenadas alrededor de la zona seleccionada (distribución normal)
                double latitud = zonaSeleccionada.Lat + (rand.NextDouble() - 0.5) * 0.02;
                double longitud = zonaSeleccionada.Lon + (rand.NextDouble() - 0.5) * 0.03;

                // Asegurar que esté dentro de los límites
                latitud = Math.Max(latMin, Math.Min(latMax, latitud));
                longitud = Math.Max(lonMin, Math.Min(lonMax, longitud));

                // Generar datos del cliente
                string tipoNegocio = tiposNegocio[rand.Next(tiposNegocio.Length)];
                string calle = calles[rand.Next(calles.Length)];
                string nombre = $"{tipoNegocio} {zonaSeleccionada.Nombre} {i}";

                // Ventas promedio: entre 1000 y 10000 Bs.
                decimal promedioVentas = (decimal)(rand.NextDouble() * 9000 + 1000);
                promedioVentas = Math.Round(promedioVentas, 2);

                // Tiempo en PDV: entre 15 y 60 minutos
                int tiempoPdv = rand.Next(15, 61);

                var cliente = new ClienteOrigenGis
                {
                    ClienteId = i,
                    Nombre = nombre,
                    Latitud = latitud,
                    Longitud = longitud,
                    PromedioVentas = promedioVentas,
                    TiempoPdv = tiempoPdv,
                    Direccion = $"{calle} #{rand.Next(100, 999)}, {zonaSeleccionada.Nombre}"
                };

                clientes.Add(cliente);
            }

            log.Info($"Generados {clientes.Count} clientes de demostración en La Paz");
            return clientes;
        }

        
        public static List<ClienteOrigenGis> GetListCustomerLocation()
        {
            var repo = new CustomerRepository();
            var listCustomers = repo.GetAll();

            var clientes = new List<ClienteOrigenGis>();

            foreach ( var c in listCustomers )
            {
                var cliente = new ClienteOrigenGis
                {
                    ClienteId = c.ClienteId,
                    Nombre = c.NombreCompleto,
                    Latitud = c.Latitud,
                    Longitud = c.Longitud,
                    PromedioVentas = 0,
                    TiempoPdv = 2,
                    Direccion = string.Empty
                };

                clientes.Add(cliente);
            }
            
            log.Info($"Generados {clientes.Count} clientes de demostración en La Paz");
            return clientes;
        }

        #endregion
    }
}

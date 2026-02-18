using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Web.UI;

namespace PrototypeRouteOSM.Pages
{
    public partial class MapClustering : Page
    {
        // Clase que representa un pedido
        public class Pedido
        {
            public int Id { get; set; }
            public string Cliente { get; set; }
            public double Lat { get; set; }
            public double Lon { get; set; }
            public int Demanda { get; set; } // Unidades que ocupa (ej. peso, volumen)
        }

        // Clase para representar un clúster con asignación
        public class ClusterAsignado
        {
            public int Id { get; set; }
            public List<Pedido> Pedidos { get; set; }
            public double CentroLat { get; set; }
            public double CentroLon { get; set; }
            public string Dia { get; set; }
            public string Repartidor { get; set; }
            public int DemandaTotal => Pedidos.Sum(p => p.Demanda);
        }

        // Parámetros de DBSCAN
        private const double EPS = 800; // metros
        private const int MIN_PTS = 2;

        // Lista de pedidos (simulada)
        private List<Pedido> pedidos = new List<Pedido>();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                GenerarPedidosEjemplo();
                Session["Pedidos"] = pedidos;
            }
            else
            {
                if (Session["Pedidos"] != null)
                    pedidos = (List<Pedido>)Session["Pedidos"];
            }
        }

        private void GenerarPedidosEjemplo()
        {
            // Centros de las zonas en La Paz (lat, lon)
            var centros = new (double lat, double lon)[]
            {
                (-16.495, -68.133), // Centro
                (-16.508, -68.127), // Sopocachi
                (-16.515, -68.115), // Miraflores
                (-16.525, -68.108), // San Jorge
                (-16.535, -68.120)  // Obrajes
            };

            var random = new Random();
            int id = 1;
            foreach (var centro in centros)
            {
                for (int i = 0; i < 6; i++) // 6 puntos por zona = 30 total
                {
                    // Desviación de ±0.002 grados (~200 metros)
                    double lat = centro.lat + (random.NextDouble() - 0.5) * 0.004;
                    double lon = centro.lon + (random.NextDouble() - 0.5) * 0.004;
                    pedidos.Add(new Pedido
                    {
                        Id = id++,
                        Cliente = $"Cliente {id}",
                        Lat = lat,
                        Lon = lon,
                        Demanda = random.Next(1, 5)
                    });
                }
            }
        }

        // Método llamado desde la página para obtener JSON de clústeres asignados
        public string GetClustersJson()
        {
            var clusters = EjecutarAgrupacion();
            var json = new JavaScriptSerializer().Serialize(clusters);
            return json;
        }

        protected void btnRecluster_Click(object sender, EventArgs e)
        {
            // Forzar re-ejecución (la asignación se recalcula en GetClustersJson)
            // Solo refrescamos la página
            Response.Redirect(Request.RawUrl);
        }

        private List<ClusterAsignado> EjecutarAgrupacion()
        {
            // 1. Aplicar DBSCAN para obtener clústeres geográficos
            var clustersGeo = DBSCAN(pedidos, EPS, MIN_PTS);

            // 2. Asignar clústeres a días y repartidores con capacidad
            var clustersAsignados = AsignarDiasYRepartidores(clustersGeo);

            return clustersAsignados;
        }

        // Implementación de DBSCAN
        private List<List<Pedido>> DBSCAN(List<Pedido> puntos, double eps, int minPts)
        {
            var visitado = new bool[puntos.Count];
            var clusters = new List<List<Pedido>>();

            for (int i = 0; i < puntos.Count; i++)
            {
                if (visitado[i]) continue;
                visitado[i] = true;

                var vecinos = ObtenerVecinos(puntos, i, eps);
                if (vecinos.Count < minPts)
                {
                    // Se considera ruido (no se agrupa)
                    continue;
                }

                // Nuevo clúster
                var cluster = new List<Pedido> { puntos[i] };
                clusters.Add(cluster);

                // Expandir clúster
                var semilla = new Queue<int>(vecinos);
                while (semilla.Count > 0)
                {
                    int j = semilla.Dequeue();
                    if (!visitado[j])
                    {
                        visitado[j] = true;
                        var vecinosJ = ObtenerVecinos(puntos, j, eps);
                        if (vecinosJ.Count >= minPts)
                        {
                            foreach (var k in vecinosJ)
                                if (!semilla.Contains(k) && !cluster.Contains(puntos[k]))
                                    semilla.Enqueue(k);
                        }
                    }
                    if (!cluster.Contains(puntos[j]))
                        cluster.Add(puntos[j]);
                }
            }
            return clusters;
        }

        private List<int> ObtenerVecinos(List<Pedido> puntos, int idx, double eps)
        {
            var vecinos = new List<int>();
            for (int i = 0; i < puntos.Count; i++)
            {
                if (i == idx) continue;
                double d = Haversine(puntos[idx].Lat, puntos[idx].Lon, puntos[i].Lat, puntos[i].Lon);
                if (d <= eps)
                    vecinos.Add(i);
            }
            return vecinos;
        }

        // Fórmula de Haversine para distancia en metros entre dos puntos geográficos
        private double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // radio de la Tierra en metros
            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double grados) => grados * Math.PI / 180;

        // Asignación de clústeres a días y repartidores
        private List<ClusterAsignado> AsignarDiasYRepartidores(List<List<Pedido>> clustersGeo)
        {
            var asignados = new List<ClusterAsignado>();

            // Definimos días laborales (de lunes a viernes)
            var dias = new List<string> { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes" };

            // Capacidad máxima por repartidor (en unidades de demanda)
            int capacidadRepartidor = 10;

            // Número de repartidores disponibles por día
            int repartidoresPorDia = 2;

            // Ordenamos clústeres por demanda total descendente para asignar primero los más grandes
            var clustersOrdenados = clustersGeo.Select((c, idx) => new
            {
                Id = idx + 1,
                Pedidos = c,
                DemandaTotal = c.Sum(p => p.Demanda)
            }).OrderByDescending(x => x.DemandaTotal).ToList();

            int clusterIndex = 0;
            foreach (var dia in dias)
            {
                for (int r = 1; r <= repartidoresPorDia; r++)
                {
                    if (clusterIndex >= clustersOrdenados.Count) break;

                    var cluster = clustersOrdenados[clusterIndex];
                    asignados.Add(new ClusterAsignado
                    {
                        Id = cluster.Id,
                        Pedidos = cluster.Pedidos,
                        CentroLat = cluster.Pedidos.Average(p => p.Lat),
                        CentroLon = cluster.Pedidos.Average(p => p.Lon),
                        Dia = dia,
                        Repartidor = $"Repartidor {r}"
                    });
                    clusterIndex++;
                }
                if (clusterIndex >= clustersOrdenados.Count) break;
            }

            // Si quedan clústeres sin asignar (pocos días), se asignan al último día disponible
            while (clusterIndex < clustersOrdenados.Count)
            {
                var cluster = clustersOrdenados[clusterIndex];
                asignados.Add(new ClusterAsignado
                {
                    Id = cluster.Id,
                    Pedidos = cluster.Pedidos,
                    CentroLat = cluster.Pedidos.Average(p => p.Lat),
                    CentroLon = cluster.Pedidos.Average(p => p.Lon),
                    Dia = "Viernes",
                    Repartidor = $"Repartidor Extra"
                });
                clusterIndex++;
            }

            return asignados;
        }
    }
}
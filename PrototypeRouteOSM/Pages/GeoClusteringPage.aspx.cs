using PrototypeRouteOSM.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.UI;

namespace PrototypeRouteOSM.Pages
{
    public partial class GeoClusteringPage : Page
    {
        // ── Muestra de puntos de referencia (Bolivia / Latinoamérica) ──────────
        private static readonly List<GeoPoint> SamplePoints = new List<GeoPoint>
        {
            // La Paz, Bolivia
            new GeoPoint( 1, -16.5000, -68.1193, "Plaza Murillo"),
            new GeoPoint( 2, -16.4897, -68.1193, "Zona Sur"),
            new GeoPoint( 3, -16.5050, -68.1300, "Sopocachi"),
            new GeoPoint( 4, -16.5100, -68.1150, "Miraflores"),
            new GeoPoint( 5, -16.4950, -68.1250, "San Miguel"),
            new GeoPoint( 6, -16.5200, -68.1050, "Obrajes"),
            new GeoPoint( 7, -16.5150, -68.0900, "Calacoto"),
            new GeoPoint( 8, -16.5300, -68.0800, "Achumani"),
            // Cochabamba
            new GeoPoint( 9, -17.3895, -66.1568, "Plaza Cochabamba"),
            new GeoPoint(10, -17.4000, -66.1500, "Quillacollo"),
            new GeoPoint(11, -17.3800, -66.1600, "Norte Cbba"),
            new GeoPoint(12, -17.3700, -66.1700, "El Abra"),
            // Santa Cruz
            new GeoPoint(13, -17.7833, -63.1822, "Casco Viejo"),
            new GeoPoint(14, -17.7700, -63.1950, "Equipetrol"),
            new GeoPoint(15, -17.7900, -63.2000, "Urubó"),
            new GeoPoint(16, -17.8000, -63.1700, "Plan 3000"),
            // Oruro
            new GeoPoint(17, -17.9667, -67.1167, "Plaza Oruro"),
            new GeoPoint(18, -17.9700, -67.1100, "Villa Fátima Oru"),
            // Potosí
            new GeoPoint(19, -19.5836, -65.7531, "Cerro Rico"),
            new GeoPoint(20, -19.5700, -65.7600, "Centro Potosí"),
        };

        // ─────────────────────────────────────────────────────────────────────
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Inicializar dropdowns
                ddlAlgorithm.Items.Clear();
                ddlAlgorithm.Items.Add("K-Means");
                ddlAlgorithm.Items.Add("DBSCAN");
                ddlAlgorithm.Items.Add("Sweep Line");

                ddlK.Items.Clear();
                for (int i = 2; i <= 8; i++)
                    ddlK.Items.Add(i.ToString());

                ddlK.SelectedValue = "4";

                // Mostrar puntos de muestra en JSON para el mapa
                HiddenPoints.Value = PointsToJson(SamplePoints, null);
                HiddenClusters.Value = "[]";
                HiddenCentroids.Value = "[]";
                LblStatus.Text = "Seleccione un algoritmo y presione <strong>Ejecutar</strong>.";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        protected void BtnRun_Click(object sender, EventArgs e)
        {
            try
            {
                ClusterResult result;
                string algo = ddlAlgorithm.SelectedValue;

                switch (algo)
                {
                    case "K-Means":
                        int k = int.Parse(ddlK.SelectedValue);
                        result = new KMeansClustering(k).Fit(SamplePoints);
                        break;

                    case "DBSCAN":
                        double eps = double.Parse(TxtEpsilon.Text.Replace(',', '.'),
                                                     System.Globalization.CultureInfo.InvariantCulture);
                        int minPts = int.Parse(TxtMinPts.Text);
                        result = new DBSCANClustering(eps, minPts).Fit(SamplePoints);
                        break;

                    case "Sweep Line":
                        double lonBand = double.Parse(TxtLonBand.Text.Replace(',', '.'),
                                                      System.Globalization.CultureInfo.InvariantCulture);
                        double latBand = double.Parse(TxtLatBand.Text.Replace(',', '.'),
                                                      System.Globalization.CultureInfo.InvariantCulture);
                        result = new SweepLineClustering(lonBand, latBand).Fit(SamplePoints);
                        break;

                    default:
                        throw new InvalidOperationException("Algoritmo desconocido.");
                }

                // Serializar resultado para JavaScript / Leaflet
                HiddenPoints.Value = PointsToJson(result.Points, result.Clusters);
                HiddenClusters.Value = ClustersToJson(result.Clusters);
                HiddenCentroids.Value = CentroidsToJson(result.Centroids);

                // Resumen estadístico
                var sb = new StringBuilder();
                sb.AppendFormat("<strong>Algoritmo:</strong> {0} &nbsp;|&nbsp; ", result.Algorithm);
                sb.AppendFormat("<strong>Grupos:</strong> {0} &nbsp;|&nbsp; ", result.TotalGroups);
                sb.AppendFormat("<strong>Puntos:</strong> {0} &nbsp;|&nbsp; ", result.Points.Count);
                sb.AppendFormat("<strong>Tiempo:</strong> {0:F3} ms", result.ElapsedTime.TotalMilliseconds);

                int noiseCount = result.Clusters.ContainsKey(-1)
                               ? result.Clusters[-1].Count : 0;
                if (noiseCount > 0)
                    sb.AppendFormat(" &nbsp;|&nbsp; <span class='text-warning'><strong>Ruido:</strong> {0}</span>",
                                    noiseCount);

                LblStatus.Text = sb.ToString();

                // Tabla de clusters
                BuildClusterTable(result);
            }
            catch (Exception ex)
            {
                LblStatus.Text = $"<span class='text-danger'><strong>Error:</strong> {ex.Message}</span>";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        private void BuildClusterTable(ClusterResult result)
        {
            var sb = new StringBuilder();
            sb.Append("<table class='table table-sm table-striped table-bordered'>");
            sb.Append("<thead class='table-dark'><tr>");
            sb.Append("<th>Cluster</th><th>Puntos</th><th>Lat. Centroide</th><th>Lon. Centroide</th><th>Miembros</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var kvp in result.Clusters.OrderBy(k => k.Key))
            {
                string clusterName = kvp.Key == -1 ? "🔴 Ruido/Outlier" : $"#{kvp.Key}";
                var centroid = result.Centroids?.FirstOrDefault(c => c.Id == kvp.Key);
                string latStr = centroid != null ? centroid.Latitude.ToString("F6") : "—";
                string lonStr = centroid != null ? centroid.Longitude.ToString("F6") : "—";
                string members = string.Join(", ", kvp.Value.Select(p =>
                    string.IsNullOrEmpty(p.Label) ? $"P{p.Id}" : p.Label));

                sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td style='max-width:300px;font-size:0.8em'>{4}</td></tr>",
                                clusterName, kvp.Value.Count, latStr, lonStr, members);
            }

            sb.Append("</tbody></table>");
            LitClusterTable.Text = sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers de serialización JSON (sin dependencias externas)
        // ─────────────────────────────────────────────────────────────────────
        private static string PointsToJson(List<GeoPoint> pts,
                                           Dictionary<int, List<GeoPoint>> clusters)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                if (i > 0) sb.Append(",");
                sb.AppendFormat(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"id\":{0},\"lat\":{1},\"lon\":{2},\"label\":\"{3}\",\"cluster\":{4}}}",
                    p.Id, p.Latitude, p.Longitude,
                    (p.Label ?? "").Replace("\"", "\\\""),
                    p.ClusterId);
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string ClustersToJson(Dictionary<int, List<GeoPoint>> clusters)
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var kvp in clusters)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.AppendFormat("{{\"id\":{0},\"count\":{1}}}", kvp.Key, kvp.Value.Count);
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string CentroidsToJson(List<GeoPoint> centroids)
        {
            if (centroids == null) return "[]";
            var sb = new StringBuilder("[");
            for (int i = 0; i < centroids.Count; i++)
            {
                var c = centroids[i];
                if (i > 0) sb.Append(",");
                sb.AppendFormat(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"id\":{0},\"lat\":{1},\"lon\":{2},\"label\":\"{3}\"}}",
                    c.Id, c.Latitude, c.Longitude,
                    (c.Label ?? "").Replace("\"", "\\\""));
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}

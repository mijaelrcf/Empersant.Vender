using System;
using System.Collections.Generic;
using System.Linq;

namespace PrototypeRouteOSM.Utilities
{
    // ─────────────────────────────────────────────────────────────
    //  Modelo base de localización geográfica
    // ─────────────────────────────────────────────────────────────
    public class GeoPoint
    {
        public int Id { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Label { get; set; }
        public int ClusterId { get; set; } = -1; // -1 = sin asignar / ruido

        public GeoPoint() { }
        public GeoPoint(int id, double lat, double lon, string label = "")
        {
            Id = id;
            Latitude = lat;
            Longitude = lon;
            Label = label;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Resultado genérico de clustering
    // ─────────────────────────────────────────────────────────────
    public class ClusterResult
    {
        public string Algorithm { get; set; }
        public List<GeoPoint> Points { get; set; }
        public Dictionary<int, List<GeoPoint>> Clusters { get; set; }
        public List<GeoPoint> Centroids { get; set; }
        public int TotalGroups { get; set; }
        public TimeSpan ElapsedTime { get; set; }
    }

    // ─────────────────────────────────────────────────────────────
    //  Utilidades de distancia
    // ─────────────────────────────────────────────────────────────
    public static class GeoDistance
    {
        private const double EarthRadiusKm = 6371.0;

        /// <summary>Distancia Haversine en kilómetros entre dos puntos geográficos.</summary>
        public static double Haversine(GeoPoint a, GeoPoint b)
        {
            double dLat = ToRad(b.Latitude - a.Latitude);
            double dLon = ToRad(b.Longitude - a.Longitude);
            double sinLat = Math.Sin(dLat / 2);
            double sinLon = Math.Sin(dLon / 2);

            double h = sinLat * sinLat
                     + Math.Cos(ToRad(a.Latitude))
                     * Math.Cos(ToRad(b.Latitude))
                     * sinLon * sinLon;

            return 2 * EarthRadiusKm * Math.Asin(Math.Sqrt(h));
        }

        /// <summary>Distancia Euclidiana simple (útil para coordenadas locales / pruebas).</summary>
        public static double Euclidean(GeoPoint a, GeoPoint b)
        {
            double dLat = a.Latitude - b.Latitude;
            double dLon = a.Longitude - b.Longitude;
            return Math.Sqrt(dLat * dLat + dLon * dLon);
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
    }

    // ═════════════════════════════════════════════════════════════
    //  1. K-MEANS CLUSTERING
    //     Particiona N puntos en K grupos minimizando la inercia.
    //     Iterativo: asignación → recálculo de centroides.
    // ═════════════════════════════════════════════════════════════
    public class KMeansClustering
    {
        private readonly int _k;
        private readonly int _maxIterations;
        private readonly double _tolerance;
        private readonly Random _rng;

        /// <param name="k">Número de clusters deseados.</param>
        /// <param name="maxIterations">Límite de iteraciones (default 300).</param>
        /// <param name="tolerance">Cambio mínimo en centroides para converger (km).</param>
        /// <param name="seed">Semilla aleatoria para reproducibilidad.</param>
        public KMeansClustering(int k, int maxIterations = 300,
                                double tolerance = 0.0001, int seed = 42)
        {
            if (k <= 0) throw new ArgumentException("k debe ser > 0");
            _k = k;
            _maxIterations = maxIterations;
            _tolerance = tolerance;
            _rng = new Random(seed);
        }

        public ClusterResult Fit(List<GeoPoint> points)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("La lista de puntos está vacía.");
            if (_k > points.Count)
                throw new ArgumentException("k no puede ser mayor que el número de puntos.");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Clonar puntos para no modificar los originales
            var pts = points.Select(p => new GeoPoint(p.Id, p.Latitude, p.Longitude, p.Label)).ToList();

            // Inicialización: K-Means++ para mejor convergencia
            var centroids = InitKMeansPlusPlus(pts);

            for (int iter = 0; iter < _maxIterations; iter++)
            {
                // ── Paso 1: Asignar cada punto al centroide más cercano ──
                foreach (var p in pts)
                    p.ClusterId = ClosestCentroid(p, centroids);

                // ── Paso 2: Recalcular centroides ──
                var newCentroids = new List<GeoPoint>();
                bool converged = true;

                for (int c = 0; c < _k; c++)
                {
                    var members = pts.Where(p => p.ClusterId == c).ToList();
                    if (members.Count == 0)
                    {
                        // Centroide vacío: reubicarlo aleatoriamente
                        newCentroids.Add(pts[_rng.Next(pts.Count)]);
                        converged = false;
                        continue;
                    }

                    var nc = new GeoPoint
                    {
                        Id = c,
                        Latitude = members.Average(p => p.Latitude),
                        Longitude = members.Average(p => p.Longitude),
                        Label = $"Centroid-{c}"
                    };
                    newCentroids.Add(nc);

                    if (GeoDistance.Haversine(centroids[c], nc) > _tolerance)
                        converged = false;
                }

                centroids = newCentroids;
                if (converged) break;
            }

            sw.Stop();

            // Construir resultado
            var clusters = pts
                .GroupBy(p => p.ClusterId)
                .ToDictionary(g => g.Key, g => g.ToList());

            return new ClusterResult
            {
                Algorithm = "K-Means",
                Points = pts,
                Clusters = clusters,
                Centroids = centroids,
                TotalGroups = clusters.Count,
                ElapsedTime = sw.Elapsed
            };
        }

        // ── K-Means++ initialization ─────────────────────────────
        private List<GeoPoint> InitKMeansPlusPlus(List<GeoPoint> pts)
        {
            var centroids = new List<GeoPoint>();
            centroids.Add(pts[_rng.Next(pts.Count)]);

            for (int c = 1; c < _k; c++)
            {
                // Distancia cuadrada al centroide más cercano
                double[] dSq = pts
                    .Select(p => Math.Pow(MinDistToCentroids(p, centroids), 2))
                    .ToArray();

                double sum = dSq.Sum();
                double r = _rng.NextDouble() * sum;
                double acc = 0;

                for (int i = 0; i < pts.Count; i++)
                {
                    acc += dSq[i];
                    if (acc >= r) { centroids.Add(pts[i]); break; }
                }
            }
            return centroids;
        }

        private double MinDistToCentroids(GeoPoint p, List<GeoPoint> centroids)
            => centroids.Min(c => GeoDistance.Haversine(p, c));

        private int ClosestCentroid(GeoPoint p, List<GeoPoint> centroids)
        {
            int best = 0;
            double min = double.MaxValue;
            for (int i = 0; i < centroids.Count; i++)
            {
                double d = GeoDistance.Haversine(p, centroids[i]);
                if (d < min) { min = d; best = i; }
            }
            return best;
        }
    }

    // ═════════════════════════════════════════════════════════════
    //  2. DBSCAN  (Density-Based Spatial Clustering of
    //              Applications with Noise)
    //     Descubre clusters de densidad arbitraria.
    //     Puntos con < minPts vecinos en radio ε → ruido (id = -1).
    // ═════════════════════════════════════════════════════════════
    public class DBSCANClustering
    {
        private readonly double _epsilonKm;
        private readonly int _minPts;

        private const int UNVISITED = -2;
        private const int NOISE = -1;

        /// <param name="epsilonKm">Radio de vecindad en kilómetros.</param>
        /// <param name="minPts">Mínimo de puntos para ser core point.</param>
        public DBSCANClustering(double epsilonKm = 0.5, int minPts = 3)
        {
            if (epsilonKm <= 0) throw new ArgumentException("epsilon debe ser > 0");
            if (minPts <= 0) throw new ArgumentException("minPts debe ser > 0");
            _epsilonKm = epsilonKm;
            _minPts = minPts;
        }

        public ClusterResult Fit(List<GeoPoint> points)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("La lista de puntos está vacía.");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var pts = points
                .Select(p => new GeoPoint(p.Id, p.Latitude, p.Longitude, p.Label)
                { ClusterId = UNVISITED })
                .ToList();

            int clusterId = 0;

            foreach (var p in pts)
            {
                if (p.ClusterId != UNVISITED) continue;

                var neighbors = GetNeighbors(p, pts);

                if (neighbors.Count < _minPts)
                {
                    p.ClusterId = NOISE;
                    continue;
                }

                // Expandir cluster
                p.ClusterId = clusterId;
                var seeds = new Queue<GeoPoint>(neighbors.Where(n => n.Id != p.Id));

                while (seeds.Count > 0)
                {
                    var q = seeds.Dequeue();
                    if (q.ClusterId == NOISE) q.ClusterId = clusterId;
                    if (q.ClusterId != UNVISITED) continue;

                    q.ClusterId = clusterId;
                    var qNeighbors = GetNeighbors(q, pts);
                    if (qNeighbors.Count >= _minPts)
                        foreach (var nb in qNeighbors.Where(n => n.ClusterId == UNVISITED || n.ClusterId == NOISE))
                            seeds.Enqueue(nb);
                }

                clusterId++;
            }

            sw.Stop();

            var clusters = pts
                .GroupBy(p => p.ClusterId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Centroides por cluster (excluir ruido)
            var centroids = clusters
                .Where(kvp => kvp.Key >= 0)
                .Select(kvp => new GeoPoint
                {
                    Id = kvp.Key,
                    Latitude = kvp.Value.Average(p => p.Latitude),
                    Longitude = kvp.Value.Average(p => p.Longitude),
                    Label = $"DBSCAN-Centroid-{kvp.Key}"
                }).ToList();

            return new ClusterResult
            {
                Algorithm = "DBSCAN",
                Points = pts,
                Clusters = clusters,
                Centroids = centroids,
                TotalGroups = clusters.Count(kvp => kvp.Key >= 0),
                ElapsedTime = sw.Elapsed
            };
        }

        private List<GeoPoint> GetNeighbors(GeoPoint p, List<GeoPoint> pts)
            => pts.Where(q => GeoDistance.Haversine(p, q) <= _epsilonKm).ToList();
    }

    // ═════════════════════════════════════════════════════════════
    //  3. SWEEP LINE ALGORITHM (Grid / Sweep)
    //     Ordenar puntos por longitud (eje X) y agrupar en
    //     franjas de ancho configurable.  Dentro de cada franja
    //     se sub-agrupa por latitud. Complejidad O(n log n).
    //     Ideal para datos distribuidos en cuadrícula.
    // ═════════════════════════════════════════════════════════════
    public class SweepLineClustering
    {
        private readonly double _lonBandWidth; // grados de longitud por franja vertical
        private readonly double _latBandWidth; // grados de latitud por franja horizontal

        /// <param name="lonBandWidth">Ancho de franja longitudinal en grados (default 0.05 ≈ 5.5 km).</param>
        /// <param name="latBandWidth">Ancho de franja latitudinal en grados  (default 0.05 ≈ 5.5 km).</param>
        public SweepLineClustering(double lonBandWidth = 0.05, double latBandWidth = 0.05)
        {
            if (lonBandWidth <= 0) throw new ArgumentException("lonBandWidth debe ser > 0");
            if (latBandWidth <= 0) throw new ArgumentException("latBandWidth debe ser > 0");
            _lonBandWidth = lonBandWidth;
            _latBandWidth = latBandWidth;
        }

        public ClusterResult Fit(List<GeoPoint> points)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("La lista de puntos está vacía.");

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var pts = points
                .Select(p => new GeoPoint(p.Id, p.Latitude, p.Longitude, p.Label))
                .ToList();

            double minLon = pts.Min(p => p.Longitude);
            double minLat = pts.Min(p => p.Latitude);

            int clusterId = 0;

            // Asignar cluster mediante índice de celda 2-D
            var cellMap = new Dictionary<(int, int), int>();

            foreach (var p in pts)
            {
                int ci = (int)Math.Floor((p.Longitude - minLon) / _lonBandWidth);
                int ri = (int)Math.Floor((p.Latitude - minLat) / _latBandWidth);
                var key = (ci, ri);

                if (!cellMap.ContainsKey(key))
                    cellMap[key] = clusterId++;

                p.ClusterId = cellMap[key];
            }

            sw.Stop();

            var clusters = pts
                .GroupBy(p => p.ClusterId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var centroids = clusters.Select(kvp => new GeoPoint
            {
                Id = kvp.Key,
                Latitude = kvp.Value.Average(p => p.Latitude),
                Longitude = kvp.Value.Average(p => p.Longitude),
                Label = $"Sweep-Cell-{kvp.Key}"
            }).ToList();

            return new ClusterResult
            {
                Algorithm = "Sweep Line",
                Points = pts,
                Clusters = clusters,
                Centroids = centroids,
                TotalGroups = clusters.Count,
                ElapsedTime = sw.Elapsed
            };
        }
    }
}

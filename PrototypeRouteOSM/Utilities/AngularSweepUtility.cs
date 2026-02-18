using Detesim.Vender.ClienteOrigenGis;
using Detesim.Vender.ZonaGisOrigen;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PrototypeRouteOSM.Utilities
{
    public class AngularSweepUtility
    {
        private static readonly ILog log = LogManager.GetLogger("Standard");

        public static class GeneradorRutasSweepTabu
        {
            /// <summary>
            /// Genera zonas/rutas compactas:
            /// 1) Angular Sweep (orden por ángulo)
            /// 2) Sweep Balanceado (cupos por cantidad)
            /// 3) Boundary Swaps + Tabu Search (mejora cortes sin perder contigüidad)
            /// </summary>
            public static List<ZonaGisOrigen> GenerarRutas(
                List<ClienteOrigenGis> clientes,
                int numeroZonas,
                int tabuIteraciones = 2000,
                int tabuTenure = 40
            )
            {
                if (clientes == null)
                {
                    log.Error("GenerarRutas: la lista de clientes es null");
                    return null;
                }

                if (clientes.Count == 0)
                {
                    log.Warn("GenerarRutas: la lista de clientes esta vacia");
                    return new List<ZonaGisOrigen>();
                }

                if (numeroZonas <= 0)
                {
                    log.Error("GenerarRutas: numeroZonas debe ser > 0");
                    return null;
                }

                numeroZonas = Math.Min(numeroZonas, clientes.Count);

                // ====== 1) Seed global y orden angular (Angular Sweep) ======
                var seed = CalcularCentroide(clientes);

                var ordenados = clientes
                    .Select(
                        c =>
                            new ClienteAng
                            {
                                C = c,
                                Ang = CalcularAngulo(seed.Item1, seed.Item2, c.Latitud, c.Longitud)
                            }
                    )
                    .OrderBy(x => x.Ang)
                    .ToList();

                // ====== 2) Cupos balanceados por cantidad (Sweep Balanceado) ======
                int baseCupo = ordenados.Count / numeroZonas;
                int extras = ordenados.Count % numeroZonas;

                int[] cupos = new int[numeroZonas];
                for (int i = 0; i < numeroZonas; i++)
                    cupos[i] = baseCupo + (i < extras ? 1 : 0);

                // ====== 3) Construir zonas como segmentos contiguos del orden angular ======
                var zonas = new List<ZonaGisOrigen>(numeroZonas);
                int idx = 0;

                for (int z = 0; z < numeroZonas; z++)
                {
                    var zona = new ZonaGisOrigen { ZonaId = z + 1 };
                    for (int k = 0; k < cupos[z]; k++)
                        zona.Clientes.Add(ordenados[idx++].C);

                    RecalcularCentro(zona);
                    zonas.Add(zona);
                }

                // ====== 4) Boundary Swaps + Tabu Search ======
                TabuBoundarySwaps(zonas, cupos, tabuIteraciones, tabuTenure);

                return zonas;
            }

            // ============================================================
            // TABU SEARCH: solo swaps en frontera (zonas adyacentes)
            // ============================================================
            private static void TabuBoundarySwaps(
                List<ZonaGisOrigen> zonas,
                int[] cupos,
                int maxIter,
                int tenure
            )
            {
                // --- Inicia los acumuladores para cada zona
                var zonaStats = zonas.Select(z => new ZonaAcc(z)).ToList();
                double curScore = zonaStats.Sum(z => z.Score);
                double bestScore = curScore;
                var bestSnapshot = Snapshot(zonas);

                var tabu = new Dictionary<string, int>();
                var rnd = new Random(1);

                int sinMejorar = 0;
                const int maxSinMejora = 300; // early stop si no mejora

                for (int iter = 1; iter <= maxIter; iter++)
                {
                    SwapMove bestMove = null;
                    double bestDelta = double.PositiveInfinity;

                    for (int i = 0; i < zonas.Count - 1; i++)
                    {
                        var a = zonas[i];
                        var b = zonas[i + 1];
                        if (a.Clientes.Count == 0 || b.Clientes.Count == 0)
                            continue;

                        // Solo frontera. Puedes poner K > 1 si te da el tiempo, pero K=1 es más rápido y sigue siendo útil
                        var ca = a.Clientes[a.Clientes.Count - 1];
                        var cb = b.Clientes[0];

                        string key = TabuKey(i, ca.ClienteId, cb.ClienteId);
                        int until;
                        bool isTabu = tabu.TryGetValue(key, out until) && until >= iter;
                        bool aspiration = false;

                        // Evalua delta más rápido: solo calculamos las diferencias en score (después de aplicar el swap en memoria temporal):
                        var A = zonaStats[i];
                        var B = zonaStats[i + 1];

                        // simular cambio
                        // Remueve y swap en la lista real SOLO para score temporal
                        A.Zona.Clientes[A.Zona.Clientes.Count - 1] = cb;
                        B.Zona.Clientes[0] = ca;
                        A.SwapOutIn(ca, cb);
                        B.SwapOutIn(cb, ca);

                        double delta =
                            (A.Score + B.Score) - (zonaStats[i].Score + zonaStats[i + 1].Score);

                        // Deshacer los swaps (vuelve a dejar la lista como estaba y recalcula los centroides)
                        A.Zona.Clientes[A.Zona.Clientes.Count - 1] = ca;
                        B.Zona.Clientes[0] = cb;
                        A.SwapOutIn(cb, ca);
                        B.SwapOutIn(ca, cb);

                        aspiration = (curScore + delta) < bestScore;

                        if ((!isTabu || aspiration) && delta < bestDelta)
                        {
                            bestDelta = delta;
                            bestMove = new SwapMove
                            {
                                BorderIndex = i,
                                A = ca,
                                B = cb
                            };
                        }
                    }

                    if (bestMove == null)
                        break;

                    // Aplica el mejor swap encontrado
                    int idx = bestMove.BorderIndex;
                    var zonaA = zonas[idx];
                    var zonaB = zonas[idx + 1];
                    int idxA = zonaA.Clientes.FindIndex(x => x.ClienteId == bestMove.A.ClienteId);
                    int idxB = zonaB.Clientes.FindIndex(x => x.ClienteId == bestMove.B.ClienteId);
                    // Swap real en lista
                    zonaA.Clientes[idxA] = bestMove.B;
                    zonaB.Clientes[idxB] = bestMove.A;

                    // Actualiza acumuladores
                    zonaStats[idx].SwapOutIn(bestMove.A, bestMove.B);
                    zonaStats[idx + 1].SwapOutIn(bestMove.B, bestMove.A);

                    // Atualiza score
                    curScore = zonaStats.Sum(z => z.Score);

                    // Marcar tabu (ambas direcciones)
                    int expire = iter + tenure + rnd.Next(0, 5);
                    tabu[
                        TabuKey(bestMove.BorderIndex, bestMove.A.ClienteId, bestMove.B.ClienteId)
                    ] = expire;
                    tabu[
                        TabuKey(bestMove.BorderIndex, bestMove.B.ClienteId, bestMove.A.ClienteId)
                    ] = expire;

                    if (curScore < bestScore)
                    {
                        bestScore = curScore;
                        bestSnapshot = Snapshot(zonas);
                        sinMejorar = 0;
                    }
                    else
                    {
                        sinMejorar++;
                        if (sinMejorar > maxSinMejora)
                            break; // early stop
                    }

                    // Limpieza básica del tabu map (opcional)
                    if (iter % 200 == 0 && tabu.Count > 5000)
                    {
                        var keysToRemove = tabu.Where(kv => kv.Value < iter)
                            .Select(kv => kv.Key)
                            .ToList();
                        foreach (var k in keysToRemove)
                            tabu.Remove(k);
                    }
                }

                // Restaurar mejor solución encontrada
                Restore(zonas, bestSnapshot);
            }

            // ============================================================
            // SCORE / DELTA / SWAP HELPERS
            // ============================================================

            // Suma de distancias (metros) desde cada cliente al centro de su zona.
            // (Compactación: cuanto menor, más compacta la zona)
            private static double Score(List<ZonaGisOrigen> zonas)
            {
                double s = 0.0;
                foreach (var z in zonas)
                {
                    // asegurar centro actualizado
                    if (z.Clientes.Count == 0)
                        continue;
                    // (si ya recalculas en cada swap, puedes omitir)
                    foreach (var c in z.Clientes)
                        s += HaversineMeters(z.CentroLat, z.CentroLon, c.Latitud, c.Longitud);
                }
                return s;
            }

            // Delta del score si swap(ca, cb) entre zona i y i+1
            private static double DeltaSwap(
                List<ZonaGisOrigen> zonas,
                int i,
                ClienteOrigenGis ca,
                ClienteOrigenGis cb
            )
            {
                var A = zonas[i];
                var B = zonas[i + 1];

                // score actual de A y B
                double before = ScoreZone(A) + ScoreZone(B);

                // simular swap (sin tocar listas reales)
                var listA = new List<ClienteOrigenGis>(A.Clientes);
                var listB = new List<ClienteOrigenGis>(B.Clientes);

                int idxA = listA.FindIndex(x => x.ClienteId == ca.ClienteId);
                int idxB = listB.FindIndex(x => x.ClienteId == cb.ClienteId);
                if (idxA < 0 || idxB < 0)
                    return double.PositiveInfinity;

                listA[idxA] = cb;
                listB[idxB] = ca;

                var centerA = Centroide(listA);
                var centerB = Centroide(listB);

                double after =
                    ScoreList(listA, centerA.Item1, centerA.Item2)
                    + ScoreList(listB, centerB.Item1, centerB.Item2);

                return after - before;
            }

            private static double ScoreZone(ZonaGisOrigen z)
            {
                if (z.Clientes.Count == 0)
                    return 0.0;
                return ScoreList(z.Clientes, z.CentroLat, z.CentroLon);
            }

            private static double ScoreList(
                List<ClienteOrigenGis> list,
                double clat,
                double clon
            )
            {
                double s = 0;
                foreach (var c in list)
                    s += HaversineMeters(clat, clon, c.Latitud, c.Longitud);
                return s;
            }

            private static void ApplySwap(
                List<ZonaGisOrigen> zonas,
                int i,
                ClienteOrigenGis ca,
                ClienteOrigenGis cb
            )
            {
                var A = zonas[i];
                var B = zonas[i + 1];

                int idxA = A.Clientes.FindIndex(x => x.ClienteId == ca.ClienteId);
                int idxB = B.Clientes.FindIndex(x => x.ClienteId == cb.ClienteId);

                if (idxA < 0 || idxB < 0)
                    return;

                A.Clientes[idxA] = cb;
                B.Clientes[idxB] = ca;

                RecalcularCentro(A);
                RecalcularCentro(B);
            }

            private static string TabuKey(int borderIndex, int aId, int bId)
            {
                return borderIndex.ToString() + "|" + aId.ToString() + ">" + bId.ToString();
            }

            private sealed class SwapMove
            {
                public int BorderIndex;
                public ClienteOrigenGis A; // from zona i
                public ClienteOrigenGis B; // from zona i+1
            }

            // ============================================================
            // SNAPSHOT / RESTORE
            // ============================================================
            private static List<List<int>> Snapshot(List<ZonaGisOrigen> zonas)
            {
                // snapshot por ClienteId (mantiene asignación 1-1)
                return zonas.Select(z => z.Clientes.Select(c => c.ClienteId).ToList()).ToList();
            }

            private static void Restore(
                List<ZonaGisOrigen> zonas,
                List<List<int>> snap
            )
            {
                // reconstruye listas a partir de ids actuales
                var map = zonas.SelectMany(z => z.Clientes).ToDictionary(c => c.ClienteId, c => c);

                for (int i = 0; i < zonas.Count; i++)
                {
                    zonas[i].Clientes = snap[i].Select(id => map[id]).ToList();
                    RecalcularCentro(zonas[i]);
                }
            }

            // ============================================================
            // GEO HELPERS
            // ============================================================
            private sealed class ClienteAng
            {
                public ClienteOrigenGis C;
                public double Ang;
            }

            private static Tuple<double, double> CalcularCentroide(
                List<ClienteOrigenGis> clientes
            )
            {
                double lat = clientes.Average(x => x.Latitud);
                double lon = clientes.Average(x => x.Longitud);

                return new Tuple<double, double>(lat, lon);
            }

            private static Tuple<double, double> Centroide(
                List<ClienteOrigenGis> clientes
            )
            {
                double lat = clientes.Average(x => x.Latitud);
                double lon = clientes.Average(x => x.Longitud);

                return new Tuple<double, double>(lat, lon);
            }

            private static void RecalcularCentro(ZonaGisOrigen z)
            {
                if (z.Clientes.Count == 0)
                {
                    z.CentroLat = 0;
                    z.CentroLon = 0;
                    return;
                }
                z.CentroLat = z.Clientes.Average(c => c.Latitud);
                z.CentroLon = z.Clientes.Average(c => c.Longitud);
            }

            // Ángulo polar respecto a seed (0..2pi), estable con corrección cos(latSeed)
            private static double CalcularAngulo(
                double latSeed,
                double lonSeed,
                double lat,
                double lon
            )
            {
                double dx = (lon - lonSeed) * Math.Cos(ToRad(latSeed));
                double dy = (lat - latSeed);
                double ang = Math.Atan2(dy, dx);
                if (ang < 0)
                    ang += 2 * Math.PI;
                return ang;
            }

            private static double HaversineMeters(
                double lat1,
                double lon1,
                double lat2,
                double lon2
            )
            {
                const double R = 6371000.0; // metros
                double dLat = ToRad(lat2 - lat1);
                double dLon = ToRad(lon2 - lon1);
                double a =
                    Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(ToRad(lat1))
                        * Math.Cos(ToRad(lat2))
                        * Math.Sin(dLon / 2)
                        * Math.Sin(dLon / 2);
                double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                return R * c;
            }

            private static double ToRad(double deg)
            {
                return deg * Math.PI / 180.0;
            }

            // Nueva clase para memorizar Score/Centroide incremental de zona
            private sealed class ZonaAcc
            {
                public ZonaGisOrigen Zona;
                public double SumaLat,
                    SumaLon;
                public int N;
                public double CentroLat,
                    CentroLon;
                public double Score; // Suma de distancias cliente-centro

                public ZonaAcc(ZonaGisOrigen z)
                {
                    Zona = z;
                    N = z.Clientes.Count;
                    SumaLat = z.Clientes.Sum(c => c.Latitud);
                    SumaLon = z.Clientes.Sum(c => c.Longitud);
                    CentroLat = SumaLat / N;
                    CentroLon = SumaLon / N;
                    Score = z.Clientes.Sum(
                        c => HaversineMeters(CentroLat, CentroLon, c.Latitud, c.Longitud)
                    );
                }

                // Actualiza sumas y Score tras un swap
                public void SwapOutIn(
                    ClienteOrigenGis qSale,
                    ClienteOrigenGis qEntra
                )
                {
                    SumaLat += qEntra.Latitud - qSale.Latitud;
                    SumaLon += qEntra.Longitud - qSale.Longitud;
                    CentroLat = SumaLat / N;
                    CentroLon = SumaLon / N;
                    // Actualiza Score (solo cambia para los dos clientes swappeados)
                    Score -= HaversineMeters(CentroLat, CentroLon, qSale.Latitud, qSale.Longitud);
                    Score += HaversineMeters(CentroLat, CentroLon, qEntra.Latitud, qEntra.Longitud);
                }
            }
        }
    }
}

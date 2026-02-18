<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="GeoClusteringPage.aspx.cs" Inherits="PrototypeRouteOSM.Pages.GeoClusteringPage" %>

<!DOCTYPE html>
<html lang="es">
<head runat="server">
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Geo Clustering — OSM · .NET</title>

    <!-- Bootstrap 5 -->
    <link rel="stylesheet"
          href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" />

    <!-- Leaflet CSS -->
    <link rel="stylesheet"
          href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />

    <style>
        :root {
            --accent: #0d6efd;
            --panel-bg: #f8f9fa;
        }

        body { background: #eef0f3; font-family: 'Segoe UI', sans-serif; }

        /* ── Top bar ─────────────────────────────────────────── */
        .topbar {
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 60%, #0f3460 100%);
            color: #fff;
            padding: 14px 24px;
            display: flex;
            align-items: center;
            gap: 14px;
            box-shadow: 0 2px 8px rgba(0,0,0,.4);
        }
        .topbar .logo { font-size: 1.5rem; font-weight: 700; letter-spacing: 1px; }
        .topbar .sub  { font-size: .75rem; opacity: .7; }
        .topbar .badge-osm {
            background: #7cc24d; color: #fff;
            font-size: .68rem; padding: 3px 8px;
            border-radius: 20px; font-weight: 600;
        }

        /* ── Layout ──────────────────────────────────────────── */
        .main-grid {
            display: grid;
            grid-template-columns: 320px 1fr;
            gap: 0;
            height: calc(100vh - 58px);
        }

        /* ── Side panel ──────────────────────────────────────── */
        .side-panel {
            background: var(--panel-bg);
            border-right: 1px solid #dee2e6;
            overflow-y: auto;
            padding: 16px;
        }

        .section-title {
            font-size: .7rem;
            font-weight: 700;
            text-transform: uppercase;
            letter-spacing: .08em;
            color: #6c757d;
            margin: 12px 0 6px;
        }

        .algo-card {
            border: 2px solid transparent;
            border-radius: 8px;
            padding: 10px 12px;
            margin-bottom: 8px;
            background: #fff;
            cursor: pointer;
            transition: all .2s;
        }
        .algo-card:hover, .algo-card.active {
            border-color: var(--accent);
            background: #e8f0fe;
        }
        .algo-card .algo-name { font-weight: 600; font-size: .9rem; }
        .algo-card .algo-desc { font-size: .75rem; color: #6c757d; margin-top: 2px; }

        .param-box {
            background: #fff;
            border: 1px solid #dee2e6;
            border-radius: 8px;
            padding: 12px;
            margin-top: 6px;
        }

        /* ── Map ─────────────────────────────────────────────── */
        .map-wrap {
            display: flex;
            flex-direction: column;
        }

        #map {
            flex: 1;
            min-height: 0;
        }

        /* ── Status bar ──────────────────────────────────────── */
        .status-bar {
            background: #212529;
            color: #adb5bd;
            font-size: .78rem;
            padding: 6px 14px;
            min-height: 32px;
        }

        /* ── Results panel ───────────────────────────────────── */
        .results-panel {
            height: 220px;
            overflow-y: auto;
            background: #fff;
            border-top: 1px solid #dee2e6;
            padding: 10px 16px;
        }

        /* ── Legend ──────────────────────────────────────────── */
        .legend-dot {
            width: 12px; height: 12px;
            border-radius: 50%;
            display: inline-block;
            margin-right: 4px;
        }
        .legend-wrap {
            position: absolute;
            bottom: 24px; right: 10px;
            z-index: 999;
            background: rgba(255,255,255,.92);
            border-radius: 8px;
            padding: 8px 12px;
            font-size: .75rem;
            box-shadow: 0 2px 6px rgba(0,0,0,.18);
            max-height: 200px;
            overflow-y: auto;
        }

        /* ── Responsive ──────────────────────────────────────── */
        @media (max-width: 768px) {
            .main-grid { grid-template-columns: 1fr; grid-template-rows: auto 1fr; }
            .side-panel { height: auto; max-height: 45vh; }
        }
    </style>
</head>
<body>

<form id="form1" runat="server">

    <!-- Hidden fields for JS communication -->
    <asp:HiddenField ID="HiddenPoints"    runat="server" />
    <asp:HiddenField ID="HiddenClusters"  runat="server" />
    <asp:HiddenField ID="HiddenCentroids" runat="server" />

    <!-- ── Top Bar ──────────────────────────────────────────── -->
    <div class="topbar">
        <div>
            <div class="logo">📍 GeoCluster</div>
            <div class="sub">Algoritmos de Agrupación de Localizaciones</div>
        </div>
        <span class="badge-osm">OpenStreetMap</span>
        <span class="badge-osm" style="background:#e76f51">.NET Framework</span>
    </div>

    <!-- ── Main Layout ──────────────────────────────────────── -->
    <div class="main-grid">

        <!-- ══ Side Panel ════════════════════════════════════ -->
        <div class="side-panel">

            <div class="section-title">🧠 Algoritmo</div>

            <!-- Algorithm selector cards -->
            <div id="algoKmeans"  class="algo-card active" onclick="selectAlgo('K-Means')">
                <div class="algo-name">K-Means Clustering</div>
                <div class="algo-desc">Divide N puntos en K grupos por cercanía al centroide.</div>
            </div>
            <div id="algoDbscan"  class="algo-card" onclick="selectAlgo('DBSCAN')">
                <div class="algo-name">DBSCAN</div>
                <div class="algo-desc">Clustering por densidad, detecta outliers automáticamente.</div>
            </div>
            <div id="algoSweep"   class="algo-card" onclick="selectAlgo('Sweep Line')">
                <div class="algo-name">Sweep Line</div>
                <div class="algo-desc">Cuadrícula geoespacial — O(n log n).</div>
            </div>

            <!-- Hidden dropdowns sync with cards -->
            <asp:DropDownList ID="ddlAlgorithm" runat="server"
                              CssClass="d-none" />

            <!-- ── K-Means params ─────────────────────────── -->
            <div id="paramsKmeans" class="param-box">
                <div class="section-title">Parámetros K-Means</div>
                <label class="form-label small">Número de clusters (K)</label>
                <asp:DropDownList ID="ddlK" runat="server" CssClass="form-select form-select-sm" />
            </div>

            <!-- ── DBSCAN params ──────────────────────────── -->
            <div id="paramsDbscan" class="param-box" style="display:none">
                <div class="section-title">Parámetros DBSCAN</div>
                <label class="form-label small">ε Epsilon (km)</label>
                <asp:TextBox ID="TxtEpsilon" runat="server" Text="150"
                             CssClass="form-control form-control-sm mb-2" />
                <label class="form-label small">MinPts (vecinos mínimos)</label>
                <asp:TextBox ID="TxtMinPts" runat="server" Text="2"
                             CssClass="form-control form-control-sm" />
            </div>

            <!-- ── Sweep params ───────────────────────────── -->
            <div id="paramsSweep" class="param-box" style="display:none">
                <div class="section-title">Parámetros Sweep Line</div>
                <label class="form-label small">Ancho de franja longitud (°)</label>
                <asp:TextBox ID="TxtLonBand" runat="server" Text="2"
                             CssClass="form-control form-control-sm mb-2" />
                <label class="form-label small">Ancho de franja latitud (°)</label>
                <asp:TextBox ID="TxtLatBand" runat="server" Text="2"
                             CssClass="form-control form-control-sm" />
            </div>

            <!-- ── Run button ─────────────────────────────── -->
            <asp:Button ID="BtnRun" runat="server" Text="▶ Ejecutar Clustering"
                        OnClick="BtnRun_Click"
                        CssClass="btn btn-primary w-100 mt-3" />

            <!-- ── Info boxes ─────────────────────────────── -->
            <div class="section-title mt-3">ℹ️ Descripción</div>
            <div id="infoKmeans" class="small text-muted" style="line-height:1.5">
                <strong>K-Means</strong> asigna cada punto al centroide más cercano y
                recalcula iterativamente. Requiere definir <em>K</em> a priori. 
                Usa inicialización <em>K-Means++</em> para mayor estabilidad.
            </div>
            <div id="infoDbscan" class="small text-muted d-none" style="line-height:1.5">
                <strong>DBSCAN</strong> marca como <em>core point</em> todo punto con ≥
                MinPts vecinos dentro del radio ε. Los puntos no alcanzables son
                <span class="text-danger">ruido</span>. No necesita K.
            </div>
            <div id="infoSweep" class="small text-muted d-none" style="line-height:1.5">
                <strong>Sweep Line</strong> divide el espacio en celdas regulares por
                longitud y latitud. Asigna el mismo cluster a todos los puntos de la
                misma celda. Muy rápido para grandes volúmenes.
            </div>
        </div>

        <!-- ══ Map + Results ════════════════════════════════════ -->
        <div class="map-wrap position-relative">

            <!-- Leaflet map -->
            <div id="map"></div>

            <!-- Floating legend -->
            <div class="legend-wrap" id="legendWrap" style="display:none">
                <strong style="font-size:.8rem">Clusters</strong>
                <div id="legendItems" class="mt-1"></div>
            </div>

            <!-- Status bar -->
            <div class="status-bar">
                <asp:Label ID="LblStatus" runat="server" />
            </div>

            <!-- Results table -->
            <div class="results-panel">
                <div class="section-title">📊 Tabla de Resultados</div>
                <asp:Literal ID="LitClusterTable" runat="server" />
            </div>
        </div>

    </div><!-- /main-grid -->

</form>

<!-- ── Scripts ─────────────────────────────────────────────── -->
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"></script>
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>

<script>
// ─── Colores de cluster ────────────────────────────────────────
const COLORS = [
    '#3b82f6','#22c55e','#f59e0b','#ef4444','#8b5cf6',
    '#06b6d4','#f97316','#ec4899','#10b981','#6366f1',
    '#84cc16','#e11d48','#0ea5e9','#d97706','#7c3aed'
];
const NOISE_COLOR = '#6b7280';

// ─── Mapa ──────────────────────────────────────────────────────
const map = L.map('map', { zoomControl: true })
             .setView([-17.5, -65.5], 6);

L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    maxZoom: 19
}).addTo(map);

let markersLayer   = L.layerGroup().addTo(map);
let centroidsLayer = L.layerGroup().addTo(map);

// ─── Renderizar puntos ────────────────────────────────────────
function renderMap() {
    const rawPoints    = document.getElementById('<%=HiddenPoints.ClientID%>').value;
    const rawCentroids = document.getElementById('<%=HiddenCentroids.ClientID%>').value;

    markersLayer.clearLayers();
    centroidsLayer.clearLayers();

    let points    = [];
    let centroids = [];

    try { points    = JSON.parse(rawPoints);    } catch(e){}
    try { centroids = JSON.parse(rawCentroids); } catch(e){}

    if (!points.length) return;

    const clusterIds = [...new Set(points.map(p => p.cluster))].sort((a,b) => a - b);
    const colored    = points.some(p => p.cluster !== -1 && p.cluster !== undefined);

    // ── Draw points ───────────────────────────────────────────
    points.forEach(p => {
        const color = p.cluster === -1
            ? NOISE_COLOR
            : COLORS[p.cluster % COLORS.length];

        const marker = L.circleMarker([p.lat, p.lon], {
            radius:      7,
            fillColor:   color,
            color:       '#fff',
            weight:      1.5,
            fillOpacity: 0.85
        });
        marker.bindPopup(
            `<strong>${p.label || 'Punto ' + p.id}</strong><br/>` +
            `Cluster: ${p.cluster === -1 ? '🔴 Ruido' : '#' + p.cluster}<br/>` +
            `Lat: ${p.lat.toFixed(6)}<br/>Lon: ${p.lon.toFixed(6)}`
        );
        markersLayer.addLayer(marker);
    });

    // ── Draw centroids ────────────────────────────────────────
    centroids.forEach(c => {
        const color = COLORS[c.id % COLORS.length];
        const icon  = L.divIcon({
            html: `<div style="background:${color};width:18px;height:18px;
                   border-radius:50%;border:3px solid #fff;
                   box-shadow:0 0 0 2px ${color}"></div>`,
            iconSize:   [18, 18],
            iconAnchor: [ 9,  9],
            className:  ''
        });
        const m = L.marker([c.lat, c.lon], { icon });
        m.bindPopup(`<strong>Centroide #${c.id}</strong><br/>${c.label}<br/>` +
                    `Lat: ${c.lat.toFixed(6)}<br/>Lon: ${c.lon.toFixed(6)}`);
        centroidsLayer.addLayer(m);
    });

    // ── Legend ────────────────────────────────────────────────
    const legendWrap  = document.getElementById('legendWrap');
    const legendItems = document.getElementById('legendItems');
    legendItems.innerHTML = '';

    if (colored) {
        legendWrap.style.display = 'block';
        clusterIds.forEach(id => {
            const color = id === -1 ? NOISE_COLOR : COLORS[id % COLORS.length];
            const label = id === -1 ? 'Ruido / Outlier' : `Cluster #${id}`;
            legendItems.innerHTML +=
                `<div><span class="legend-dot" style="background:${color}"></span>${label}</div>`;
        });
    } else {
        legendWrap.style.display = 'none';
    }

    // ── Fit bounds ────────────────────────────────────────────
    const bounds = L.latLngBounds(points.map(p => [p.lat, p.lon]));
    map.fitBounds(bounds, { padding: [30, 30] });
}

// ─── Algorithm card selection ─────────────────────────────────
function selectAlgo(name) {
    const ddl = document.getElementById('<%=ddlAlgorithm.ClientID%>');
    for (let i = 0; i < ddl.options.length; i++)
        if (ddl.options[i].value === name) { ddl.selectedIndex = i; break; }

    document.querySelectorAll('.algo-card').forEach(c => c.classList.remove('active'));
    document.getElementById('paramsKmeans').style.display = 'none';
    document.getElementById('paramsDbscan').style.display = 'none';
    document.getElementById('paramsSweep' ).style.display = 'none';
    document.getElementById('infoKmeans').classList.add('d-none');
    document.getElementById('infoDbscan').classList.add('d-none');
    document.getElementById('infoSweep' ).classList.add('d-none');

    if (name === 'K-Means') {
        document.getElementById('algoKmeans').classList.add('active');
        document.getElementById('paramsKmeans').style.display = '';
        document.getElementById('infoKmeans').classList.remove('d-none');
    } else if (name === 'DBSCAN') {
        document.getElementById('algoDbscan').classList.add('active');
        document.getElementById('paramsDbscan').style.display = '';
        document.getElementById('infoDbscan').classList.remove('d-none');
    } else {
        document.getElementById('algoSweep').classList.add('active');
        document.getElementById('paramsSweep').style.display = '';
        document.getElementById('infoSweep').classList.remove('d-none');
    }
}

// ─── Init ─────────────────────────────────────────────────────
window.addEventListener('load', function() {
    renderMap();

    // Redraw after postback
    const form = document.getElementById('form1');
    if (form) {
        form.addEventListener('submit', function() {
            setTimeout(renderMap, 100);
        });
    }
});

// Re-render after ASP.NET postback
if (typeof Sys !== 'undefined') {
    Sys.WebForms.PageRequestManager.getInstance()
       .add_endRequest(function() { renderMap(); });
}
</script>

</body>
</html>


<%@ Page Title="Generar Zonas Angular - Demo La Paz" Language="C#" MasterPageFile="~/Pages/Site.Master" AutoEventWireup="true" CodeBehind="RouteMapAngularSweep.aspx.cs" Inherits="PrototypeRouteOSM.Pages.RouteMapAngularSweep" %>

<asp:Content ID="Content1" ContentPlaceHolderID="HeadContent" runat="Server">
    <!-- Leaflet CSS (OpenStreetMap) -->
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    
    <style type="text/css">
        #map {
            width: 100%;
            height: 600px;
            border: 2px solid #ddd;
            border-radius: 8px;
        }
        
        .zona-stats {
            background: #f8f9fa;
            padding: 10px;
            border-radius: 5px;
            margin-bottom: 10px;
        }
        
        .zona-header {
            display: flex;
            align-items: center;
            gap: 10px;
            margin-bottom: 5px;
        }
        
        .zona-color {
            width: 20px;
            height: 20px;
            border-radius: 50%;
            display: inline-block;
        }
        
        .info-panel {
            background: white;
            padding: 15px;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
            margin-bottom: 20px;
        }
        
        .metric {
            display: inline-block;
            margin-right: 20px;
        }
        
        .metric-value {
            font-size: 24px;
            font-weight: bold;
            color: #007bff;
        }
        
        .metric-label {
            font-size: 12px;
            color: #6c757d;
        }
    </style>

    <!-- Leaflet JavaScript -->
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    
    <script type="text/javascript">
        var map;
        var markers = [];
        var layerGroups = {};
        var routeLines = {}; // Líneas de ruta por zona

        // Colores para cada zona (7 colores distintos)
        var zonaColors = {
            1: '#e74c3c', // Rojo
            2: '#27ae60', // Verde
            3: '#3498db', // Azul
            4: '#f39c12', // Naranja
            5: '#9b59b6', // Púrpura
            6: '#e91e63', // Rosa
            7: '#ff9800'  // Naranja oscuro
        };

        function initMap() {
            // Centro de La Paz, Bolivia
            var laPazCenter = [-16.5000, -68.1500];

            // Crear el mapa
            map = L.map('map').setView(laPazCenter, 12);

            // Añadir capa de OpenStreetMap
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
                maxZoom: 19
            }).addTo(map);

            // Obtener datos de zonas desde el HiddenField
            var rutaDataElement = document.getElementById('<%= MapHF.ClientID %>');

            if (!rutaDataElement || !rutaDataElement.value || rutaDataElement.value.length === 0) {
                console.log("No hay datos de rutas para mostrar");
                return;
            }

            var rutaData = JSON.parse(rutaDataElement.value);

            if (!rutaData || rutaData.length === 0) {
                console.log("Array de rutas vacío");
                return;
            }

            // Crear un grupo de capas para cada zona
            for (var i = 1; i <= 7; i++) {
                layerGroups[i] = L.layerGroup().addTo(map);
                routeLines[i] = [];
            }

            // Organizar clientes por zona (manteniendo el orden original)
            var clientesPorZona = {};
            for (var i = 0; i < rutaData.length; i++) {
                var cliente = rutaData[i];
                if (!clientesPorZona[cliente.ZonaId]) {
                    clientesPorZona[cliente.ZonaId] = [];
                }
                clientesPorZona[cliente.ZonaId].push(cliente);
            }

            var bounds = [];

            // Procesar cada zona
            for (var zonaId in clientesPorZona) {
                var clientes = clientesPorZona[zonaId];
                var color = zonaColors[zonaId] || '#999999';
                var routeCoords = [];

                // Crear marcadores numerados para cada cliente
                for (var i = 0; i < clientes.length; i++) {
                    var cliente = clientes[i];
                    var orden = i + 1; // Número de orden de visita

                    // Crear icono con número
                    var customIcon = L.divIcon({
                        className: 'custom-marker-numbered',
                        html: '<div style="background-color:' + color + '; width: 32px; height: 32px; border-radius: 50%; border: 3px solid white; box-shadow: 0 2px 5px rgba(0,0,0,0.3); display: flex; align-items: center; justify-content: center; color: white; font-weight: bold; font-size: 14px;">' + orden + '</div>',
                        iconSize: [32, 32],
                        iconAnchor: [16, 16]
                    });

                    // Crear marcador
                    var marker = L.marker([cliente.Latitud, cliente.Longitud], {
                        icon: customIcon,
                        zIndexOffset: 1000 // Marcadores sobre las líneas
                    }).addTo(layerGroups[zonaId]);

                    // Crear popup con información del cliente
                    var popupContent = '<div style="min-width: 220px;">' +
                        '<h6 style="margin: 0 0 10px 0; color: ' + color + ';"><strong>🚩 Parada #' + orden + '</strong></h6>' +
                        '<h6 style="margin: 0 0 10px 0;"><strong>' + cliente.Nombre + '</strong></h6>' +
                        '<hr style="margin: 5px 0;">' +
                        '<p style="margin: 5px 0;"><strong>Zona:</strong> ' + zonaId + '</p>' +
                        '<p style="margin: 5px 0;"><strong>ID Cliente:</strong> ' + cliente.ClienteId + '</p>' +
                        '<p style="margin: 5px 0;"><strong>Ventas Promedio:</strong> Bs. ' + cliente.PromedioVentas.toFixed(2) + '</p>' +
                        '<p style="margin: 5px 0;"><strong>Tiempo PDV:</strong> ' + cliente.TiempoPdv + ' min</p>' +
                        '</div>';

                    marker.bindPopup(popupContent);

                    // Guardar coordenadas para la ruta
                    routeCoords.push([cliente.Latitud, cliente.Longitud]);
                    bounds.push([cliente.Latitud, cliente.Longitud]);
                    markers.push(marker);
                }

                // Dibujar línea de ruta para esta zona
                if (routeCoords.length > 1) {
                    var polyline = L.polyline(routeCoords, {
                        color: color,
                        weight: 3,
                        opacity: 0.7,
                        smoothFactor: 1
                    }).addTo(layerGroups[zonaId]);

                    // Agregar flechas direccionales (decoradores)
                    var decorator = L.polylineDecorator(polyline, {
                        patterns: [
                            {
                                offset: '10%',
                                repeat: 80,
                                symbol: L.Symbol.arrowHead({
                                    pixelSize: 10,
                                    polygon: false,
                                    pathOptions: {
                                        color: color,
                                        fillOpacity: 1,
                                        weight: 2
                                    }
                                })
                            }
                        ]
                    }).addTo(layerGroups[zonaId]);

                    routeLines[zonaId].push(polyline);
                    routeLines[zonaId].push(decorator);

                    // Calcular y mostrar distancia total de la ruta
                    var distanciaTotal = calcularDistanciaRuta(routeCoords);

                    // Crear tooltip con distancia en el punto medio de la ruta
                    var middleIndex = Math.floor(routeCoords.length / 2);
                    var middlePoint = routeCoords[middleIndex];

                    L.marker(middlePoint, {
                        icon: L.divIcon({
                            className: 'route-distance-label',
                            html: '<div style="background-color: white; padding: 5px 10px; border: 2px solid ' + color + '; border-radius: 15px; font-weight: bold; color: ' + color + '; white-space: nowrap; box-shadow: 0 2px 5px rgba(0,0,0,0.3);">📏 ' + distanciaTotal.toFixed(2) + ' km</div>',
                            iconSize: [100, 30],
                            iconAnchor: [50, 15]
                        }),
                        zIndexOffset: -100
                    }).addTo(layerGroups[zonaId]);
                }
            }

            // Ajustar el mapa para mostrar todos los puntos
            if (bounds.length > 0) {
                map.fitBounds(bounds, { padding: [50, 50] });
            }

            // Agregar control de capas para mostrar/ocultar zonas
            addLayerControl();
        }

        // Calcular distancia total de una ruta usando fórmula Haversine
        function calcularDistanciaRuta(coords) {
            var total = 0;
            for (var i = 0; i < coords.length - 1; i++) {
                total += calcularDistanciaHaversine(coords[i][0], coords[i][1], coords[i + 1][0], coords[i + 1][1]);
            }
            return total;
        }

        // Fórmula Haversine para calcular distancia en km
        function calcularDistanciaHaversine(lat1, lon1, lat2, lon2) {
            var R = 6371; // Radio de la Tierra en km
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
                Math.sin(dLon / 2) * Math.sin(dLon / 2);
            var c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
            return R * c;
        }

        function addLayerControl() {
            var overlays = {};

            for (var i = 1; i <= 7; i++) {
                var color = zonaColors[i];
                overlays['<span style="color: ' + color + '; font-weight: bold;">● Zona ' + i + ' (con ruta)</span>'] = layerGroups[i];
            }

            L.control.layers(null, overlays, { collapsed: false }).addTo(map);
        }

        // Función para centrar el mapa en una zona específica
        function zoomToZona(zonaId) {
            var zonaBounds = [];

            for (var i = 0; i < markers.length; i++) {
                var marker = markers[i];
                // Verificar si el marcador pertenece a la zona
                if (layerGroups[zonaId].hasLayer(marker)) {
                    zonaBounds.push(marker.getLatLng());
                }
            }

            if (zonaBounds.length > 0) {
                map.fitBounds(zonaBounds, { padding: [50, 50] });
            }
        }
    </script>
    
    <!-- Plugin para flechas direccionales en las rutas -->
    <script src="https://cdn.jsdelivr.net/npm/leaflet-polylinedecorator@1.6.0/dist/leaflet.polylineDecorator.min.js"></script>
</asp:Content>

<asp:Content ID="Content2" ContentPlaceHolderID="MainContent" runat="Server">
    <div class="page">
        <div class="page-inner">
            <header class="page-title-bar">
                <h1 class="page-title">🗺️ Generador de Zonas Optimizadas - La Paz, Bolivia</h1>
                <p class="text-muted">Algoritmo Angular Sweep + Tabu Search para optimización de rutas</p>
            </header>

            <div class="page-section">
                <!-- Panel de configuración -->
                <section class="card card-fluid mb-4">
                    <div class="card-header">
                        <h5>⚙️ Configuración de Zonas</h5>
                    </div>
                    <div class="card-body">
                        <div class="form-row align-items-end">
                            <div class="col-lg-3">
                                <label><strong>Número de Zonas (Días):</strong></label>
                                <asp:TextBox ID="NroDiasTB" runat="server" CssClass="form-control" 
                                    placeholder="1-7" Text="5"></asp:TextBox>
                                <small class="form-text text-muted">Ingrese un número entre 1 y 7</small>
                            </div>

                            <div class="col-lg-3">
                                <label><strong>Tipos de Negocio:</strong></label>
                                <asp:DropDownList ID="TipoNegocioDDL" runat="server" CssClass="form-control">
                                    <asp:ListItem Text="Todos" Value="" Selected="True"></asp:ListItem>
                                    <asp:ListItem Text="Retail" Value="1"></asp:ListItem>
                                    <asp:ListItem Text="Mayorista" Value="2"></asp:ListItem>
                                    <asp:ListItem Text="Supermercado" Value="3"></asp:ListItem>
                                </asp:DropDownList>
                            </div>

                            <div class="col-lg-3">
                                <asp:Button ID="GenerarZonasButton" runat="server" 
                                    CssClass="btn btn-primary btn-lg" 
                                    Text="🚀 Generar Zonas Óptimas" 
                                    OnClick="GenerarZonasButton_Click" />
                            </div>

                            <div class="col-lg-3">
                                <asp:Button ID="CargarDemoButton" runat="server" 
                                    CssClass="btn btn-success btn-lg" 
                                    Text="📍 Cargar Demo (100 clientes)" 
                                    OnClick="CargarDemoButton_Click" />
                            </div>
                        </div>
                    </div>
                </section>

                <!-- Panel de estadísticas globales -->
                <asp:Panel ID="EstadisticasPanel" runat="server" Visible="false">
                    <section class="card card-fluid mb-4">
                        <div class="card-header">
                            <h5>📊 Estadísticas Globales</h5>
                        </div>
                        <div class="card-body">
                            <div class="info-panel">
                                <div class="metric">
                                    <div class="metric-value"><asp:Label ID="TotalClientesLabel" runat="server"></asp:Label></div>
                                    <div class="metric-label">Total Clientes</div>
                                </div>
                                <div class="metric">
                                    <div class="metric-value"><asp:Label ID="TotalZonasLabel" runat="server"></asp:Label></div>
                                    <div class="metric-label">Zonas Creadas</div>
                                </div>
                                <div class="metric">
                                    <div class="metric-value"><asp:Label ID="TotalVentasLabel" runat="server"></asp:Label></div>
                                    <div class="metric-label">Ventas Totales</div>
                                </div>
                                <div class="metric">
                                    <div class="metric-value"><asp:Label ID="PromedioClientesLabel" runat="server"></asp:Label></div>
                                    <div class="metric-label">Promedio por Zona</div>
                                </div>
                            </div>
                        </div>
                    </section>
                </asp:Panel>

                <!-- Mapa -->
                <section class="card card-fluid mb-4">
                    <div class="card-header">
                        <h5>🗺️ Visualización de Zonas</h5>
                    </div>
                    <div class="card-body">
                        <div id="map"></div>
                    </div>
                </section>

                <!-- Panel de zonas generadas -->
                <asp:Panel ID="ZonasPanel" runat="server" Visible="false">
                    <section class="card card-fluid">
                        <div class="card-header">
                            <h5>📋 Detalle de Zonas Generadas</h5>
                        </div>
                        <div class="card-body">
                            <div class="table-responsive">
                                <table class="table table-striped table-bordered">
                                    <thead class="thead-dark">
                                        <tr>
                                            <th width="50">Color</th>
                                            <th>Nombre de Zona</th>
                                            <th>Día</th>
                                            <th>Clientes</th>
                                            <th>Ventas Totales</th>
                                            <th>Distancia / Tiempo</th>
                                            <th>Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <!-- Zona 1 -->
                                        <asp:Panel ID="Panel1" runat="server" Visible="false">
                                            <tr>
                                                <td style="text-align: center;">
                                                    <span class="zona-color" style="background-color: #e74c3c;"></span>
                                                </td>
                                                <td>
                                                    <asp:TextBox ID="Nombre1TB" runat="server" CssClass="form-control" 
                                                        placeholder="Ej: Zona Centro"></asp:TextBox>
                                                </td>
                                                <td>
                                                    <asp:DropDownList ID="Dia1DDL" runat="server" CssClass="form-control">
                                                        <asp:ListItem Text="Lunes" Value="1"></asp:ListItem>
                                                        <asp:ListItem Text="Martes" Value="2"></asp:ListItem>
                                                        <asp:ListItem Text="Miércoles" Value="3"></asp:ListItem>
                                                        <asp:ListItem Text="Jueves" Value="4"></asp:ListItem>
                                                        <asp:ListItem Text="Viernes" Value="5"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </td>
                                                <td><strong><asp:Label ID="Clientes1Label" runat="server"></asp:Label></strong></td>
                                                <td><asp:Label ID="Monto1Label" runat="server"></asp:Label></td>
                                                <td><asp:Label ID="Tiempo1Label" runat="server"></asp:Label></td>
                                                <td>
                                                    <button type="button" class="btn btn-sm btn-info" 
                                                        onclick="zoomToZona(1); return false;">Ver en Mapa</button>
                                                </td>
                                            </tr>
                                        </asp:Panel>

                                        <!-- Zona 2 -->
                                        <asp:Panel ID="Panel2" runat="server" Visible="false">
                                            <tr>
                                                <td style="text-align: center;">
                                                    <span class="zona-color" style="background-color: #27ae60;"></span>
                                                </td>
                                                <td>
                                                    <asp:TextBox ID="Nombre2TB" runat="server" CssClass="form-control" 
                                                        placeholder="Ej: Zona Norte"></asp:TextBox>
                                                </td>
                                                <td>
                                                    <asp:DropDownList ID="Dia2DDL" runat="server" CssClass="form-control">
                                                        <asp:ListItem Text="Lunes" Value="1"></asp:ListItem>
                                                        <asp:ListItem Text="Martes" Value="2" Selected="True"></asp:ListItem>
                                                        <asp:ListItem Text="Miércoles" Value="3"></asp:ListItem>
                                                        <asp:ListItem Text="Jueves" Value="4"></asp:ListItem>
                                                        <asp:ListItem Text="Viernes" Value="5"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </td>
                                                <td><strong><asp:Label ID="Clientes2Label" runat="server"></asp:Label></strong></td>
                                                <td><asp:Label ID="Monto2Label" runat="server"></asp:Label></td>
                                                <td><asp:Label ID="Tiempo2Label" runat="server"></asp:Label></td>
                                                <td>
                                                    <button type="button" class="btn btn-sm btn-info" 
                                                        onclick="zoomToZona(2); return false;">Ver en Mapa</button>
                                                </td>
                                            </tr>
                                        </asp:Panel>

                                        <!-- Zona 3 -->
                                        <asp:Panel ID="Panel3" runat="server" Visible="false">
                                            <tr>
                                                <td style="text-align: center;">
                                                    <span class="zona-color" style="background-color: #3498db;"></span>
                                                </td>
                                                <td>
                                                    <asp:TextBox ID="Nombre3TB" runat="server" CssClass="form-control" 
                                                        placeholder="Ej: Zona Sur"></asp:TextBox>
                                                </td>
                                                <td>
                                                    <asp:DropDownList ID="Dia3DDL" runat="server" CssClass="form-control">
                                                        <asp:ListItem Text="Lunes" Value="1"></asp:ListItem>
                                                        <asp:ListItem Text="Martes" Value="2"></asp:ListItem>
                                                        <asp:ListItem Text="Miércoles" Value="3" Selected="True"></asp:ListItem>
                                                        <asp:ListItem Text="Jueves" Value="4"></asp:ListItem>
                                                        <asp:ListItem Text="Viernes" Value="5"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </td>
                                                <td><strong><asp:Label ID="Clientes3Label" runat="server"></asp:Label></strong></td>
                                                <td><asp:Label ID="Monto3Label" runat="server"></asp:Label></td>
                                                <td><asp:Label ID="Tiempo3Label" runat="server"></asp:Label></td>
                                                <td>
                                                    <button type="button" class="btn btn-sm btn-info" 
                                                        onclick="zoomToZona(3); return false;">Ver en Mapa</button>
                                                </td>
                                            </tr>
                                        </asp:Panel>

                                        <!-- Zona 4 -->
                                        <asp:Panel ID="Panel4" runat="server" Visible="false">
                                            <tr>
                                                <td style="text-align: center;">
                                                    <span class="zona-color" style="background-color: #f39c12;"></span>
                                                </td>
                                                <td>
                                                    <asp:TextBox ID="Nombre4TB" runat="server" CssClass="form-control" 
                                                        placeholder="Ej: Zona Este"></asp:TextBox>
                                                </td>
                                                <td>
                                                    <asp:DropDownList ID="Dia4DDL" runat="server" CssClass="form-control">
                                                        <asp:ListItem Text="Lunes" Value="1"></asp:ListItem>
                                                        <asp:ListItem Text="Martes" Value="2"></asp:ListItem>
                                                        <asp:ListItem Text="Miércoles" Value="3"></asp:ListItem>
                                                        <asp:ListItem Text="Jueves" Value="4" Selected="True"></asp:ListItem>
                                                        <asp:ListItem Text="Viernes" Value="5"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </td>
                                                <td><strong><asp:Label ID="Clientes4Label" runat="server"></asp:Label></strong></td>
                                                <td><asp:Label ID="Monto4Label" runat="server"></asp:Label></td>
                                                <td><asp:Label ID="Tiempo4Label" runat="server"></asp:Label></td>
                                                <td>
                                                    <button type="button" class="btn btn-sm btn-info" 
                                                        onclick="zoomToZona(4); return false;">Ver en Mapa</button>
                                                </td>
                                            </tr>
                                        </asp:Panel>

                                        <!-- Zona 5 -->
                                        <asp:Panel ID="Panel5" runat="server" Visible="false">
                                            <tr>
                                                <td style="text-align: center;">
                                                    <span class="zona-color" style="background-color: #9b59b6;"></span>
                                                </td>
                                                <td>
                                                    <asp:TextBox ID="Nombre5TB" runat="server" CssClass="form-control" 
                                                        placeholder="Ej: Zona Oeste"></asp:TextBox>
                                                </td>
                                                <td>
                                                    <asp:DropDownList ID="Dia5DDL" runat="server" CssClass="form-control">
                                                        <asp:ListItem Text="Lunes" Value="1"></asp:ListItem>
                                                        <asp:ListItem Text="Martes" Value="2"></asp:ListItem>
                                                        <asp:ListItem Text="Miércoles" Value="3"></asp:ListItem>
                                                        <asp:ListItem Text="Jueves" Value="4"></asp:ListItem>
                                                        <asp:ListItem Text="Viernes" Value="5" Selected="True"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </td>
                                                <td><strong><asp:Label ID="Clientes5Label" runat="server"></asp:Label></strong></td>
                                                <td><asp:Label ID="Monto5Label" runat="server"></asp:Label></td>
                                                <td><asp:Label ID="Tiempo5Label" runat="server"></asp:Label></td>
                                                <td>
                                                    <button type="button" class="btn btn-sm btn-info" 
                                                        onclick="zoomToZona(5); return false;">Ver en Mapa</button>
                                                </td>
                                            </tr>
                                        </asp:Panel>

                                        <!-- Zona 6 -->
                                        <asp:Panel ID="Panel6" runat="server" Visible="false">
                                            <tr>
                                                <td style="text-align: center;">
                                                    <span class="zona-color" style="background-color: #e91e63;"></span>
                                                </td>
                                                <td>
                                                    <asp:TextBox ID="Nombre6TB" runat="server" CssClass="form-control" 
                                                        placeholder="Ej: Zona Industrial"></asp:TextBox>
                                                </td>
                                                <td>
                                                    <asp:DropDownList ID="Dia6DDL" runat="server" CssClass="form-control">
                                                        <asp:ListItem Text="Lunes" Value="1" Selected="True"></asp:ListItem>
                                                        <asp:ListItem Text="Martes" Value="2"></asp:ListItem>
                                                        <asp:ListItem Text="Miércoles" Value="3"></asp:ListItem>
                                                        <asp:ListItem Text="Jueves" Value="4"></asp:ListItem>
                                                        <asp:ListItem Text="Viernes" Value="5"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </td>
                                                <td><strong><asp:Label ID="Clientes6Label" runat="server"></asp:Label></strong></td>
                                                <td><asp:Label ID="Monto6Label" runat="server"></asp:Label></td>
                                                <td><asp:Label ID="Tiempo6Label" runat="server"></asp:Label></td>
                                                <td>
                                                    <button type="button" class="btn btn-sm btn-info" 
                                                        onclick="zoomToZona(6); return false;">Ver en Mapa</button>
                                                </td>
                                            </tr>
                                        </asp:Panel>

                                        <!-- Zona 7 -->
                                        <asp:Panel ID="Panel7" runat="server" Visible="false">
                                            <tr>
                                                <td style="text-align: center;">
                                                    <span class="zona-color" style="background-color: #ff9800;"></span>
                                                </td>
                                                <td>
                                                    <asp:TextBox ID="Nombre7TB" runat="server" CssClass="form-control" 
                                                        placeholder="Ej: Zona Periférica"></asp:TextBox>
                                                </td>
                                                <td>
                                                    <asp:DropDownList ID="Dia7DDL" runat="server" CssClass="form-control">
                                                        <asp:ListItem Text="Lunes" Value="1"></asp:ListItem>
                                                        <asp:ListItem Text="Martes" Value="2" Selected="True"></asp:ListItem>
                                                        <asp:ListItem Text="Miércoles" Value="3"></asp:ListItem>
                                                        <asp:ListItem Text="Jueves" Value="4"></asp:ListItem>
                                                        <asp:ListItem Text="Viernes" Value="5"></asp:ListItem>
                                                    </asp:DropDownList>
                                                </td>
                                                <td><strong><asp:Label ID="Clientes7Label" runat="server"></asp:Label></strong></td>
                                                <td><asp:Label ID="Monto7Label" runat="server"></asp:Label></td>
                                                <td><asp:Label ID="Tiempo7Label" runat="server"></asp:Label></td>
                                                <td>
                                                    <button type="button" class="btn btn-sm btn-info" 
                                                        onclick="zoomToZona(7); return false;">Ver en Mapa</button>
                                                </td>
                                            </tr>
                                        </asp:Panel>
                                    </tbody>
                                </table>
                            </div>

                            <div class="text-right mt-3">
                                <asp:Button ID="GuardarLB" runat="server" 
                                    CssClass="btn btn-success btn-lg" 
                                    Text="💾 Guardar Zonas" 
                                    OnClick="GuardarLB_Click" 
                                    Visible="false" />
                            </div>
                        </div>
                    </section>
                </asp:Panel>
            </div>
        </div>
    </div>

    <asp:HiddenField ID="MapHF" runat="server" />
</asp:Content>

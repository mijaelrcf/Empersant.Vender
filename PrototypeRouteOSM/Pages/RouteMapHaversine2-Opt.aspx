<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="RouteMapHaversine2-Opt.aspx.cs" Inherits="PrototypeRouteOSM.Pages.RouteMapHaversine2_Opt" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Ruta Optimizada Haversine y 2-Opt</title>
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <link rel="stylesheet" href="https://unpkg.com/leaflet-routing-machine@3.2.12/dist/leaflet-routing-machine.css" />
    <style>
        #map {
            height: 600px;
            width: 100%;
            border: 2px solid #333;
        }

        .controls {
            padding: 15px;
            background: #eee;
            border-bottom: 1px solid #ccc;
            font-family: sans-serif;
        }

        .btn-calc {
            background: #28a745;
            color: white;
            padding: 10px 20px;
            border: none;
            font-size: 16px;
            cursor: pointer;
        }

            .btn-calc:hover {
                background: #218838;
            }

        /* Estilos para los marcadores numerados */
        .numero-marcador {
            background-color: #2196F3;
            color: white;
            border-radius: 50%;
            width: 30px;
            height: 30px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            font-size: 14px;
            border: 3px solid white;
            box-shadow: 0 2px 5px rgba(0,0,0,0.3);
        }

            .numero-marcador.deposito {
                background-color: #4CAF50;
                font-size: 16px;
            }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager1" runat="server" EnablePageMethods="true" />

        <div class="controls">
            <h3>Ruta Optimizada Haversine y 2-Opt</h3>
            <button type="button" class="btn-calc" onclick="generarRuta()">📍 Trazar Ruta Óptima</button>
            <span>(Depósito simulado: Oficina Central)</span>
            <a href="/Pages/Index.aspx" class="nav-link">Volver</a>
        </div>

        <div id="map"></div>
    </form>

    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    <script src="https://unpkg.com/leaflet-routing-machine@3.2.12/dist/leaflet-routing-machine.js"></script>
    <script>
        // 1. Mapa centrado en La Paz
        var map = L.map('map').setView([-16.4700, -68.1500], 14);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: 'OSM' }).addTo(map);

        // Variables
        var clientes = [];
        var routeLayers = [];
        var marcadoresClientes = []; // Para almacenar los marcadores de clientes

        // 2. Depósito FIJO (Para no tener que hacer click manual por ahora)
        // Lo pondremos cerca de "Francisco" para iniciar
        var depositoFijo = { ClienteId: 0, NombreCompleto: "OFICINA CENTRAL", Latitud: -16.4800, Longitud: -68.1450 };

        // Función para crear un icono numerado personalizado
        function crearIconoNumerado(numero, esDeposito) {
            var className = esDeposito ? 'numero-marcador deposito' : 'numero-marcador';
            var html = esDeposito ? '🏠' : numero;

            return L.divIcon({
                className: 'custom-div-icon',
                html: '<div class="' + className + '">' + html + '</div>',
                iconSize: [30, 30],
                iconAnchor: [15, 15]
            });
        }

        // 3. Cargar datos al iniciar
        document.addEventListener("DOMContentLoaded", function () {
            // Pintar depósito
            L.marker([depositoFijo.Latitud, depositoFijo.Longitud], {
                icon: crearIconoNumerado(0, true)
            }).addTo(map)
                .bindPopup("<b>🏠 OFICINA CENTRAL</b><br>Punto de partida").openPopup();

            // Traer clientes de la BD (Simulada)
            PageMethods.GetListCustomerLocation(function (datos) {
                clientes = datos;
                datos.forEach(c => {
                    var marker = L.marker([c.Latitud, c.Longitud]).addTo(map)
                        .bindPopup("👤 " + c.NombreCompleto);
                    marcadoresClientes.push(marker);
                });
            });
        });

        var routingControl = null; // Variable global para controlar la ruta

        // 4. Acción del botón
        function generarRuta() {
            if (clientes.length === 0) return;

            // 1. Pedimos al servidor el ORDEN óptimo (lo que ya hace tu C#)
            PageMethods.CalcularRutas(clientes, depositoFijo, 1, function (rutas) {

                // Limpiar marcadores anteriores de clientes
                marcadoresClientes.forEach(m => map.removeLayer(m));
                marcadoresClientes = [];

                // Limpiar rutas previas si existen
                if (routingControl != null) {
                    map.removeControl(routingControl);
                }

                rutas.forEach(r => {
                    // 2. Crear marcadores numerados según el orden del recorrido
                    r.Puntos.forEach(function (punto) {
                        var esDeposito = punto.OrdenRecorrido === 0;
                        var icono = crearIconoNumerado(punto.OrdenRecorrido, esDeposito);

                        var popupText = esDeposito
                            ? "<b>🏠 OFICINA CENTRAL</b><br>Punto de partida (Orden: " + punto.OrdenRecorrido + ")"
                            : "<b>👤 " + punto.NombreCompleto + "</b><br>Orden de visita: " + punto.OrdenRecorrido;

                        var marker = L.marker([punto.Latitud, punto.Longitud], { icon: icono })
                            .addTo(map)
                            .bindPopup(popupText);

                        marcadoresClientes.push(marker);
                    });

                    // 3. Convertir nuestros puntos ordenados a "Waypoints" para el motor de calles
                    var puntosRuta = r.Puntos.map(p => L.latLng(p.Latitud, p.Longitud));

                    // 4. Crear el control de ruta "tipo Uber"
                    routingControl = L.Routing.control({
                        waypoints: puntosRuta,
                        router: L.Routing.osrmv1({
                            serviceUrl: 'https://router.project-osrm.org/route/v1',
                            profile: 'car' // Aquí le decimos que use reglas de automóvil (sentidos, giros, etc.)
                        }),
                        lineOptions: {
                            styles: [{ color: '#242424', opacity: 0.8, weight: 6 }] // Color oscuro tipo app moderna
                        },
                        addWaypoints: false, // Evita que el usuario mueva la ruta manualmente
                        draggableWaypoints: false,
                        fitSelectedRoutes: true,
                        show: false, // Oculta el cuadro de texto con instrucciones de manejo
                        createMarker: function () { return null; } // Usamos nuestros propios marcadores
                    }).addTo(map);
                });
            }, function (err) { alert(err.get_message()); });
        }
    </script>
</body>
</html>

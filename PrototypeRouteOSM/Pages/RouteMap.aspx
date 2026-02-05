<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="RouteMap.aspx.cs" Inherits="PrototypeRouteOSM.Pages.RouteMap" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Optimización de Rutas OSM</title>
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    
    <style>
        #map { height: 600px; width: 100%; margin-top: 20px; border: 2px solid #ccc; }
        .controls { padding: 20px; background: #f9f9f9; border-bottom: 1px solid #ddd; }
        .btn-action { padding: 10px 20px; background: #007bff; color: white; border: none; cursor: pointer; }
        .btn-action:hover { background: #0056b3; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager1" runat="server" EnablePageMethods="true" />
        
        <div class="controls">
            <h2>Gestión de Rutas con OpenStreetMap</h2>
            <label>Número de Vendedores:</label>
            <input type="number" id="txtVendedores" value="2" min="1" max="10" />
            <br /><br />
            <p><i>Haz clic en el mapa para agregar clientes (El primero será el depósito).</i></p>
            <button type="button" class="btn-action" onclick="solicitarRutas()">Calcular Rutas Óptimas</button>
            <button type="button" onclick="limpiarMapa()">Limpiar</button>
        </div>

        <div id="map"></div>
    </form>

    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    
    <script>
        // 1. Inicializar Mapa
        var map = L.map('map').setView([-16.5000, -68.1500], 13); // Coordenadas ejemplo (La Paz)

        // 2. Capa de OpenStreetMap
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);

        // Variables para guardar puntos
        var markers = [];
        var clientes = [];
        var deposito = null;
        var routeLines = []; // Para guardar las lineas dibujadas

        // Colores para diferentes vendedores
        var colores = ['blue', 'red', 'green', 'orange', 'purple'];

        // 3. Evento click en mapa para agregar clientes
        map.on('click', function(e) {
            var lat = e.latlng.lat;
            var lng = e.latlng.lng;
            
            var tipo = (deposito == null) ? "Depósito" : "Cliente " + (clientes.length + 1);
            
            // Agregar marcador visual
            var marker = L.marker([lat, lng]).addTo(map)
                .bindPopup(tipo)
                .openPopup();
            
            markers.push(marker);

            var ubicacion = { Nombre: tipo, Lat: lat, Lng: lng };

            if (deposito == null) {
                deposito = ubicacion;
                marker._icon.style.filter = "hue-rotate(120deg)"; // Cambiar color depósito
            } else {
                clientes.push(ubicacion);
            }
        });

        function limpiarMapa() {
            // Borrar marcadores y líneas
            markers.forEach(m => map.removeLayer(m));
            routeLines.forEach(l => map.removeLayer(l));
            markers = [];
            routeLines = [];
            clientes = [];
            deposito = null;
        }

        // 4. Llamada al Backend (WebForms WebMethod)
        function solicitarRutas() {
            if (!deposito || clientes.length === 0) {
                alert("Debes definir un depósito y al menos un cliente.");
                return;
            }

            var numVendedores = document.getElementById("txtVendedores").value;

            var dataToSend = {
                clientes: clientes,
                deposito: deposito,
                numVendedores: parseInt(numVendedores)
            };

            // Usamos PageMethods (nativo de WebForms para AJAX)
            PageMethods.CalcularRutas(dataToSend.clientes, dataToSend.deposito, dataToSend.numVendedores, 
                onSuccess, onError);
        }

        function onSuccess(result) {
            // Limpiar líneas anteriores
            routeLines.forEach(l => map.removeLayer(l));
            routeLines = [];

            // Dibujar nuevas rutas
            result.forEach((ruta, index) => {
                var latlngs = ruta.Puntos.map(p => [p.Lat, p.Lng]);
                var colorRuta = colores[index % colores.length];

                // Dibujar polilinea
                var polyline = L.polyline(latlngs, { color: colorRuta, weight: 5 }).addTo(map);
                routeLines.push(polyline);
                
                // Hacer zoom para ver todo
                map.fitBounds(polyline.getBounds());
            });

            alert("Rutas calculadas para " + result.length + " vendedores.");
        }

        function onError(error) {
            alert("Error al calcular: " + error.get_message());
        }
    </script>
</body>
</html>

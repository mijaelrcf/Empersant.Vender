<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="RouteClustering.aspx.cs" Inherits="PrototypeRouteOSM.Pages.RouteClustering" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Agrupación de Rutas por Días (Clustering)</title>
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <style>
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; }
        #map { height: 90vh; width: 100%; }
        .sidebar {
            height: 10vh; background: #f8f9fa; padding: 15px; display: flex; align-items: center; gap: 20px; border-bottom: 2px solid #ddd;
        }
        .btn-action {
            background-color: #007bff; color: white; border: none; padding: 10px 20px; border-radius: 5px; cursor: pointer; font-size: 16px;
        }
        .btn-action:hover { background-color: #0056b3; }
        .legend {
            background: white; padding: 10px; border-radius: 5px; box-shadow: 0 0 10px rgba(0,0,0,0.2);
            position: absolute; bottom: 30px; left: 10px; z-index: 1000;
        }
        .legend-item { display: flex; align-items: center; margin-bottom: 5px; }
        .color-box { width: 15px; height: 15px; margin-right: 8px; border-radius: 3px; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager1" runat="server" EnablePageMethods="true" />
        
        <div class="sidebar">
            <div>
                <label><b>¿En cuántos días quieres repartir?</b></label>
                <select id="ddlDias" style="padding: 8px;">
                    <option value="2">2 Días</option>
                    <option value="3" selected>3 Días</option>
                    <option value="4">4 Días</option>
                    <option value="5">5 Días (Lun-Vie)</option>
                    <option value="6">6 Días (Lun-Sab)</option>
                </select>
            </div>
            <button type="button" class="btn-action" onclick="agruparClientes()">Generar Zonas de Reparto</button>
            <div id="status" style="margin-left:auto; font-weight:bold; color:#555;">Listo</div>
        </div>

        <div id="map"></div>

        <div id="legendPanel" class="legend" style="display:none;"></div>
    </form>

    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    <script>
        var map;
        var layerGroup;
        var clientesRaw = []; // Aquí guardaremos todos los clientes cargados al inicio

        // Inicializar Mapa
        document.addEventListener("DOMContentLoaded", function () {
            map = L.map('map').setView([-16.5000, -68.1500], 13); // La Paz
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
            layerGroup = L.layerGroup().addTo(map);

            // Cargar clientes simulados o reales
            // (Aquí usarías tu método GetListCustomerLocation existente)
            // Para demo voy a generar 50 puntos aleatorios en La Paz si no hay datos
             generarDatosDemo(); 
        });

        function agruparClientes() {
            var dias = document.getElementById("ddlDias").value;
            document.getElementById("status").innerText = "Calculando zonas optimas...";

            // Llamada al Backend (K-Means)
            PageMethods.AgruparClientesPorDias(clientesRaw, parseInt(dias), function (grupos) {
                
                layerGroup.clearLayers();
                var leyendaHtml = "<h4>Zonas Asignadas</h4>";

                grupos.forEach(g => {
                    // Agregar a la leyenda
                    leyendaHtml += `
                        <div class="legend-item">
                            <div class="color-box" style="background:${g.ColorHex}"></div>
                            <span>${g.NombreDia}: <b>${g.Clientes.length} Clientes</b></span>
                        </div>`;

                    // Pintar marcadores del grupo
                    g.Clientes.forEach(c => {
                        L.circleMarker([c.Latitud, c.Longitud], {
                            radius: 8,
                            fillColor: g.ColorHex,
                            color: "#fff",
                            weight: 1,
                            opacity: 1,
                            fillOpacity: 0.8
                        }).bindPopup(`<b>${c.NombreCompleto}</b><br>Asignado a: ${g.NombreDia}`)
                          .addTo(layerGroup);
                    });
                });

                document.getElementById("legendPanel").innerHTML = leyendaHtml;
                document.getElementById("legendPanel").style.display = "block";
                document.getElementById("status").innerText = "Zonificación completada.";

            }, function (err) { alert(err.get_message()); });
        }

        // --- SOLO PARA PRUEBAS: Generador de puntos en La Paz ---
        function generarDatosDemo() {
            // Si ya tienes tu método real 'ObtenerClientes', úsalo en vez de esto
            // Simulamos 60 clientes dispersos entre El Alto y Zona Sur
            for (var i = 0; i < 60; i++) {
                var lat = -16.4500 - (Math.random() * 0.10); // Dispersión Latitud
                var lng = -68.1000 - (Math.random() * 0.10); // Dispersión Longitud
                clientesRaw.push({
                    ClienteId: i,
                    NombreCompleto: "Cliente " + i,
                    Latitud: lat,
                    Longitud: lng
                });
            }
            // Mostrar puntos iniciales en gris
            clientesRaw.forEach(c => {
                L.circleMarker([c.Latitud, c.Longitud], { color: '#999', radius: 5 }).addTo(layerGroup);
            });
        }
    </script>
</body>
</html>
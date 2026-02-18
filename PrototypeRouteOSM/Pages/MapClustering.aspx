<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="MapClustering.aspx.cs" Inherits="PrototypeRouteOSM.Pages.MapClustering" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Agrupación de Pedidos con DBSCAN</title>
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    <style>
        #map { height: 500px; width: 100%; }
        table { border-collapse: collapse; width: 100%; margin-top: 20px; }
        th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }
        th { background-color: #f2f2f2; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div>
            <h2>Agrupación de Pedidos - La Paz</h2>
            <div id="map"></div>
            <asp:Button ID="btnRecluster" runat="server" Text="Re-agrupar" OnClick="btnRecluster_Click" style="margin:10px 0;" />
            <asp:Literal ID="litResultados" runat="server"></asp:Literal>
        </div>
    </form>

    <script type="text/javascript">
        var map = L.map('map').setView([-16.5, -68.15], 13);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© OpenStreetMap contributors'
        }).addTo(map);

        var clusters = <%= GetClustersJson() %>;
        console.log(clusters); // Opcional: para depurar y ver la estructura real

        var colors = ['#FF0000', '#00FF00', '#0000FF', '#FFFF00', '#FF00FF', '#00FFFF', '#FFA500', '#800080', '#A52A2A', '#808080'];

        clusters.forEach(function (cluster, index) {
            var color = colors[index % colors.length];
            var clusterGroup = L.layerGroup().addTo(map);

            // IMPORTANTE: usar "Pedidos" con mayúscula
            cluster.Pedidos.forEach(function (pedido) {
                var marker = L.circleMarker([pedido.Lat, pedido.Lon], {
                    radius: 6,
                    color: color,
                    fillColor: color,
                    fillOpacity: 0.8
                }).bindPopup(
                    "Pedido #" + pedido.Id + "<br>" +
                    "Cliente: " + pedido.Cliente + "<br>" +
                    "Día: " + cluster.Dia + "<br>" +      // "Dia" con mayúscula
                    "Repartidor: " + cluster.Repartidor    // "Repartidor" con mayúscula
                );
                clusterGroup.addLayer(marker);
            });
        });
    </script>
</body>
</html>
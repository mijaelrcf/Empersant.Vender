<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="RouteMapHaversine.aspx.cs" Inherits="PrototypeRouteOSM.Pages.RouteMapHaversine" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Ruta Optimizada Haversine</title>
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <link rel="stylesheet" href="https://unpkg.com/leaflet-routing-machine@3.2.12/dist/leaflet-routing-machine.css" />
    <link rel="stylesheet" href="/Styles/Site.css" type="text/css" />
</head>
<body>
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager1" runat="server" EnablePageMethods="true" />

        <div class="controls">
            <h3>Ruta Optimizada Haversine</h3>
            <button type="button" class="btn-calc" onclick="generarRuta()">📍 Trazar Ruta Óptima</button>
            <span>(Depósito simulado: Oficina Central)</span>
            <a href="/Pages/Index.aspx" class="nav-link">Volver</a>
            <br />
            <div style="margin-top: 15px; font-size: 18px; font-weight: bold; color: #333;">
                Distancia Total Estimada: <span id="lblDistancia" style="color: #007bff;">0.00</span> km
            </div>
            <div style="margin-top: 15px; font-size: 18px; font-weight: bold; color: #333;">
                Tiempo Total Estimado: <span id="lblTiempo" style="color: #007bff;">00:00</span> min
            </div>
        </div>

        <div id="map"></div>
    </form>

    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
    <script src="https://unpkg.com/leaflet-routing-machine@3.2.12/dist/leaflet-routing-machine.js"></script>
    <script src="/Scripts/site.js" type="text/javascript"></script>
</body>
</html>

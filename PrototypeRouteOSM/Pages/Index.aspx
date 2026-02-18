<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Index.aspx.cs" Inherits="PrototypeRouteOSM.Pages.Index" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Prototype Route OSM</title>
    <style>
        /* Estilos Globales */
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: #f4f7f9;
            margin: 0;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            color: #333;
        }

        /* Contenedor Principal */
        .main-container {
            background-color: #ffffff;
            padding: 40px;
            border-radius: 12px;
            box-shadow: 0 10px 25px rgba(0, 0, 0, 0.1);
            text-align: center;
            width: 100%;
            max-width: 450px;
        }

        h1 {
            font-weight: 600;
            color: #2c3e50;
            margin-bottom: 30px;
            font-size: 24px;
        }

        /* Estilo de los Hipervínculos (convertidos visualmente en botones) */
        .nav-link {
            display: block;
            text-decoration: none;
            background-color: #007bff;
            color: white !important;
            padding: 12px 20px;
            margin: 10px 0;
            border-radius: 8px;
            transition: all 0.3s ease;
            font-weight: 500;
            border: none;
        }

        .nav-link:hover {
            background-color: #0056b3;
            transform: translateY(-2px);
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.15);
        }

        .footer-text {
            margin-top: 20px;
            font-size: 12px;
            color: #95a5a6;
        }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="main-container">
            <h1>Prototype Route OSM</h1>

            <div class="link-group">
                <a href="/Pages/RouteMapClickLocations.aspx" class="nav-link">Mapa de Rutas: Click Ubicaciones</a>
                <a href="/Pages/RouteMapHaversine.aspx" class="nav-link">Mapa de Rutas: Haversine</a>
                <a href="/Pages/RouteMapGoogleOrTools.aspx" class="nav-link">Mapa de Rutas: Google OR-Tools</a>
                <a href="/Pages/RouteMapHaversine2-Opt.aspx" class="nav-link">Mapa de Rutas: Haversine y 2-Opt</a>
                <a href="/Pages/RouteMapAngularSweep.aspx" class="nav-link">Mapa de Rutas: Angular Sweep</a>
            </div>

            <div class="footer-text">
                Optimization Prototypes Route OSM &copy; <%: DateTime.Now.Year %>
            </div>
        </div>
    </form>
</body>
</html>

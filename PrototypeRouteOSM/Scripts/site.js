// Variables Globales
var map;
var routingControl = null; // Variable global para controlar la ruta
var clientes = [];
var marcadoresClientes = []; // Para almacenar los marcadores de clientes
var depositoFijo = { ClienteId: 0, NombreCompleto: "OFICINA CENTRAL", Latitud: -16.4800, Longitud: -68.1450 };

// 1. Inicialización del Mapa
document.addEventListener("DOMContentLoaded", function () {
    if (document.getElementById('map')) {
        initMap();
        cargarDatosIniciales();
    }
});

function initMap() {
    // Mapa centrado en La Paz
    map = L.map('map').setView([-16.4700, -68.1500], 14);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap'
    }).addTo(map);

    // Pintar depósito inicial
    L.marker([depositoFijo.Latitud, depositoFijo.Longitud], {
        icon: crearIconoNumerado(0, true)
    }).addTo(map).bindPopup("<b>🏠 OFICINA CENTRAL</b><br>Punto de partida");
}

// 2. Iconos Numerados
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

// 3. Cargar Clientes desde el Servidor
function cargarDatosIniciales() {
    if (typeof PageMethods !== 'undefined') {
        // Traer clientes de la BD (Simulada)
        PageMethods.GetListCustomerLocation(function (datos) {
            clientes = datos;
            datos.forEach(c => {
                var marker = L.marker([c.Latitud, c.Longitud]).addTo(map)
                    .bindPopup("👤 " + c.NombreCompleto);
                marcadoresClientes.push(marker);
            });
        }, function (err) { console.error("Error al cargar clientes:", err.get_message()); });
    }
}

// 4. Lógica de Generación de Ruta
function generarRuta() {
    if (clientes.length === 0) {
        alert("No hay clientes cargados para generar la ruta.");
        return;
    }

    document.getElementById('lblDistancia').innerText = "Calculando...";
    document.getElementById('lblTiempo').innerText = "-";

    PageMethods.CalcularRutas(clientes, depositoFijo, 1, function (rutas) {

        // Limpiar mapa
        marcadoresClientes.forEach(m => map.removeLayer(m));
        marcadoresClientes = [];
        if (routingControl != null) map.removeControl(routingControl);

        rutas.forEach(r => {
            // Dibujar marcadores numerados
            r.Puntos.forEach(function (punto) {
                var esDeposito = (punto.ClienteId === 0 || punto.OrdenRecorrido === 0);
                var icono = crearIconoNumerado(punto.OrdenRecorrido, esDeposito);
                var popupText = esDeposito
                    ? "<b>🏠 INICIO</b>"
                    : "<b>👤 " + punto.NombreCompleto + "</b><br>Parada: " + punto.OrdenRecorrido;

                var marker = L.marker([punto.Latitud, punto.Longitud], { icon: icono })
                    .addTo(map)
                    .bindPopup(popupText);
                marcadoresClientes.push(marker);
            });

            // Trazar ruta por calles
            var puntosRuta = r.Puntos.map(p => L.latLng(p.Latitud, p.Longitud));

            // Crear el control de ruta "tipo Uber"
            routingControl = L.Routing.control({
                waypoints: puntosRuta,
                router: L.Routing.osrmv1({ serviceUrl: 'https://router.project-osrm.org/route/v1', profile: 'car' }),
                lineOptions: { styles: [{ color: '#242424', opacity: 0.8, weight: 6 }] },
                addWaypoints: false,
                draggableWaypoints: false,
                show: false,
                createMarker: function () { return null; }
            })
                // --- Para obtener la distancia en kilometros y el tiempo en minutos---
                .on('routesfound', function (e) {
                    var summary = e.routes[0].summary;
                    document.getElementById('lblDistancia').innerText = (summary.totalDistance / 1000).toFixed(2);
                    document.getElementById('lblTiempo').innerText = Math.round(summary.totalTime / 60);
                })
                // ----------------------------------
                .addTo(map);
        });
    }, function (err) { alert("Error en algoritmo: " + err.get_message()); });
}
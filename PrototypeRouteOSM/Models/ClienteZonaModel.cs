using System;
using System.Collections.Generic;

namespace Detesim.Vender.ClienteOrigenGis
{
    /// <summary>
    /// Representa un cliente con información geográfica (GIS)
    /// </summary>
    public class ClienteOrigenGis
    {
        public int ClienteId { get; set; }
        public string Nombre { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public decimal PromedioVentas { get; set; }
        public int TiempoPdv { get; set; } // Tiempo en punto de venta (minutos)
        public string Direccion { get; set; }
        public string TipoNegocio { get; set; }
        public string Telefono { get; set; }
        public string Email { get; set; }

        public ClienteOrigenGis()
        {
            Nombre = string.Empty;
            Direccion = string.Empty;
            TipoNegocio = string.Empty;
            Telefono = string.Empty;
            Email = string.Empty;
        }

        public override string ToString()
        {
            return $"{ClienteId} - {Nombre} ({Latitud:F6}, {Longitud:F6})";
        }
    }
}

namespace Detesim.Vender.ZonaGisOrigen
{
    /// <summary>
    /// Representa una zona/ruta con sus clientes asignados
    /// </summary>
    public class ZonaGisOrigen
    {
        public int ZonaId { get; set; }
        public string NombreZona { get; set; }
        public int DiaId { get; set; } // 1=Lunes, 2=Martes, etc.
        public double CentroLat { get; set; } // Centroide de la zona
        public double CentroLon { get; set; } // Centroide de la zona
        public List<Detesim.Vender.ClienteOrigenGis.ClienteOrigenGis> Clientes { get; set; }

        public int OrdenRecorrido { get; set; } // Agregado para ordenar la ruta

        public ZonaGisOrigen()
        {
            NombreZona = string.Empty;
            Clientes = new List<Detesim.Vender.ClienteOrigenGis.ClienteOrigenGis>();
        }

        /// <summary>
        /// Calcula el total de ventas de la zona
        /// </summary>
        public decimal TotalVentas
        {
            get
            {
                decimal total = 0;
                foreach (var cliente in Clientes)
                    total += cliente.PromedioVentas;
                return total;
            }
        }

        /// <summary>
        /// Calcula el tiempo total de visitas en la zona
        /// </summary>
        public int TotalTiempoPdv
        {
            get
            {
                int total = 0;
                foreach (var cliente in Clientes)
                    total += cliente.TiempoPdv;
                return total;
            }
        }

        /// <summary>
        /// Número de clientes en la zona
        /// </summary>
        public int NumeroClientes
        {
            get { return Clientes.Count; }
        }

        public override string ToString()
        {
            return $"Zona {ZonaId}: {NumeroClientes} clientes, Ventas: {TotalVentas:C}";
        }
    }
}

namespace Detesim.Vender.ZonaClienteGisAngular
{
    /// <summary>
    /// Representa la relación entre una zona y un cliente (para persistencia en BD)
    /// </summary>
    public class ZonaClienteGisAngular
    {
        public int Id { get; set; }
        public int ZonaId { get; set; }
        public int ClienteId { get; set; }
        public string Nombre { get; set; }
        public double Latitud { get; set; }
        public double Longitud { get; set; }
        public decimal PromedioVentas { get; set; }
        public int TiempoPdv { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaCreacion { get; set; }

        public ZonaClienteGisAngular()
        {
            Nombre = string.Empty;
            Usuario = string.Empty;
            FechaCreacion = DateTime.Now;
        }
    }
}

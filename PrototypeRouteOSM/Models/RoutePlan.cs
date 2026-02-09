using System.Collections.Generic;

namespace PrototypeRouteOSM.Models
{
    public class RoutePlan
    {
        public int VendedorId { get; set; }
        public List<CustomerLocation> Puntos { get; set; }
    }
}

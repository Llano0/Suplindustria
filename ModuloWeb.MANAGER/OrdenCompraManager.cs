using ModuloWeb.BROKER;
using ModuloWeb.ENTITIES;
 
namespace ModuloWeb.MANAGER
{
    public class OrdenCompraManager
    {
        private readonly OrdenCompraBroker broker = new OrdenCompraBroker();
 
        public List<OrdenCompra> ObtenerOrdenes()
        {
            return broker.ObtenerOrdenes();
        }
    }
}
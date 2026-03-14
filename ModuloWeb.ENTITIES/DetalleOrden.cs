namespace ModuloWeb.ENTITIES
{
    public class DetalleOrden
    {
        public int     Id                   { get; set; }
        public int     IdOrden              { get; set; }
        public int?    IdProducto           { get; set; }
        public int     Cantidad             { get; set; }
        public decimal Precio               { get; set; }
        public decimal Subtotal             { get; set; }
        // Campos completos del producto
        public string  NombreProductoManual { get; set; } = "";
        public string  Item                 { get; set; } = "";
        public string  Catalogo             { get; set; } = "";
        public string  Modelo               { get; set; } = "";
        public string  Descripcion          { get; set; } = "";
        public string  FechaEntrega         { get; set; } = "";
        public decimal Iva                  { get; set; } = 0;
        public string  Um                   { get; set; } = "UND";
        public decimal Descuento            { get; set; } = 0;
    }
}

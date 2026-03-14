namespace ModuloWeb.ENTITIES
{
    public class OrdenCompra
    {
        public int      IdOrden         { get; set; }
        public int      IdProveedor     { get; set; }
        public decimal  Total           { get; set; }
        public DateTime Fecha           { get; set; }
        public string   Estado          { get; set; } = "Pendiente";
        public string   NumeroOrden     { get; set; } = "";
        public string   NombreProveedor { get; set; } = "";
        // Campos del formulario
        public string   Condiciones     { get; set; } = "30 días";
        public string   Moneda          { get; set; } = "COP";
        public string   Comprador       { get; set; } = "";
        public string   EntregarA       { get; set; } = "SUPLINDUSTRIA S.A.S.";
        public string   EntregarAlterno { get; set; } = "NA";
        public string   Observaciones   { get; set; } = "";
    }
}
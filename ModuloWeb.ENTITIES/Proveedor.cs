namespace ModuloWeb.ENTITIES
{
    public class Proveedor
    {
        public int    Id          { get; set; }
        public string Nombre      { get; set; } = "";
        public string Nit         { get; set; } = "";
        public string Correo      { get; set; } = "";
        public string Telefono    { get; set; } = "";
        public string Direccion   { get; set; } = "";
        public string Ciudad      { get; set; } = "";
        public string Contacto    { get; set; } = "";
        public string Prefijo     { get; set; } = "";
        public int    Consecutivo { get; set; } = 0;
    }
}

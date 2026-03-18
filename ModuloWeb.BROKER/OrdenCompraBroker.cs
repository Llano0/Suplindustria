using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using ModuloWeb.ENTITIES;

namespace ModuloWeb.BROKER
{
    public class OrdenCompraBroker
    {
        public MySqlConnection CrearConexion()
        {
            var cs = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (!string.IsNullOrWhiteSpace(cs))
                return new MySqlConnection(cs);
            return ConexionBD.Conectar();
        }

        // ══════════════════════════════════════════════════════
        //  PROVEEDORES
        // ══════════════════════════════════════════════════════

        public int InsertarProveedor(Proveedor p)
        {
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "INSERT INTO proveedores (nombre, nit, correo, telefono, direccion, ciudad, contacto) " +
                "VALUES (@n,@nit,@c,@t,@d,@ciu,@cont); SELECT LAST_INSERT_ID();", con);
            cmd.Parameters.AddWithValue("@n",    p.Nombre);
            cmd.Parameters.AddWithValue("@nit",  p.Nit);
            cmd.Parameters.AddWithValue("@c",    p.Correo);
            cmd.Parameters.AddWithValue("@t",    p.Telefono);
            cmd.Parameters.AddWithValue("@d",    p.Direccion);
            cmd.Parameters.AddWithValue("@ciu",  p.Ciudad);
            cmd.Parameters.AddWithValue("@cont", p.Contacto);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void ActualizarProveedor(Proveedor p)
        {
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "UPDATE proveedores SET nombre=@n, nit=@nit, correo=@c, telefono=@t, " +
                "direccion=@d, ciudad=@ciu, contacto=@cont WHERE id=@id", con);
            cmd.Parameters.AddWithValue("@n",    p.Nombre);
            cmd.Parameters.AddWithValue("@nit",  p.Nit);
            cmd.Parameters.AddWithValue("@c",    p.Correo);
            cmd.Parameters.AddWithValue("@t",    p.Telefono);
            cmd.Parameters.AddWithValue("@d",    p.Direccion);
            cmd.Parameters.AddWithValue("@ciu",  p.Ciudad);
            cmd.Parameters.AddWithValue("@cont", p.Contacto);
            cmd.Parameters.AddWithValue("@id",   p.Id);
            cmd.ExecuteNonQuery();
        }

        public bool EliminarProveedor(int id)
        {
            using var con = CrearConexion();
            con.Open();
            var check = new MySqlCommand(
                "SELECT COUNT(*) FROM ordenes_compra WHERE id_proveedor = @id", con);
            check.Parameters.AddWithValue("@id", id);
            int ordenes = Convert.ToInt32(check.ExecuteScalar());
            if (ordenes > 0) return false;

            var cmd = new MySqlCommand("DELETE FROM proveedores WHERE id = @id", con);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return true;
        }

        public List<Proveedor> ObtenerProveedores()
        {
            var lista = new List<Proveedor>();
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "SELECT id, nombre, nit, correo, telefono, direccion, " +
                "IFNULL(ciudad,'') AS ciudad, IFNULL(contacto,'') AS contacto " +
                "FROM proveedores ORDER BY nombre", con);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) lista.Add(MapProveedor(reader));
            return lista;
        }

        public Proveedor? ObtenerProveedorPorId(int id)
        {
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "SELECT id, nombre, nit, correo, telefono, direccion, " +
                "IFNULL(ciudad,'') AS ciudad, IFNULL(contacto,'') AS contacto " +
                "FROM proveedores WHERE id = @id", con);
            cmd.Parameters.AddWithValue("@id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapProveedor(reader) : null;
        }

        private Proveedor MapProveedor(MySqlDataReader r) => new Proveedor
        {
            Id        = r.GetInt32("id"),
            Nombre    = r.GetString("nombre"),
            Nit       = r["nit"] != DBNull.Value ? r.GetString("nit") : "",
            Correo    = r.GetString("correo"),
            Telefono  = r["telefono"] != DBNull.Value ? r.GetString("telefono") : "",
            Direccion = r["direccion"] != DBNull.Value ? r.GetString("direccion") : "",
            Ciudad    = r.GetString("ciudad"),
            Contacto  = r.GetString("contacto")
        };

        // ══════════════════════════════════════════════════════
        //  ÓRDENES
        // ══════════════════════════════════════════════════════

        public int ContarOrdenesPorProveedor(int idProveedor)
        {
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM ordenes_compra WHERE id_proveedor = @id", con);
            cmd.Parameters.AddWithValue("@id", idProveedor);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void GuardarNumeroOrden(int idOrden, string numeroOrden)
        {
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "UPDATE ordenes_compra SET numero_orden = @num WHERE id_orden = @id", con);
            cmd.Parameters.AddWithValue("@num", numeroOrden);
            cmd.Parameters.AddWithValue("@id",  idOrden);
            cmd.ExecuteNonQuery();
        }

        public int InsertarOrden(int idProveedor, decimal total,
            string condiciones, string moneda, string comprador,
            string entregarA, string entregarAlterno, string observaciones)
        {
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "INSERT INTO ordenes_compra (id_proveedor, total, condiciones, moneda, comprador, " +
                "entregar_a, entregar_alterno, observaciones) " +
                "VALUES (@prov,@total,@cond,@mon,@comp,@entr,@entra,@obs); SELECT LAST_INSERT_ID();", con);
            cmd.Parameters.AddWithValue("@prov",  idProveedor);
            cmd.Parameters.AddWithValue("@total", total);
            cmd.Parameters.AddWithValue("@cond",  condiciones);
            cmd.Parameters.AddWithValue("@mon",   moneda);
            cmd.Parameters.AddWithValue("@comp",  comprador);
            cmd.Parameters.AddWithValue("@entr",  entregarA);
            cmd.Parameters.AddWithValue("@entra", entregarAlterno);
            cmd.Parameters.AddWithValue("@obs",   observaciones);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void ActualizarOrden(int idOrden, int idProveedor, decimal total,
            string condiciones, string moneda, string comprador,
            string entregarA, string entregarAlterno, string observaciones)
        {
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "UPDATE ordenes_compra SET id_proveedor=@prov, total=@total, condiciones=@cond, " +
                "moneda=@mon, comprador=@comp, entregar_a=@entr, entregar_alterno=@entra, " +
                "observaciones=@obs WHERE id_orden=@id", con);
            cmd.Parameters.AddWithValue("@prov",  idProveedor);
            cmd.Parameters.AddWithValue("@total", total);
            cmd.Parameters.AddWithValue("@cond",  condiciones);
            cmd.Parameters.AddWithValue("@mon",   moneda);
            cmd.Parameters.AddWithValue("@comp",  comprador);
            cmd.Parameters.AddWithValue("@entr",  entregarA);
            cmd.Parameters.AddWithValue("@entra", entregarAlterno);
            cmd.Parameters.AddWithValue("@obs",   observaciones);
            cmd.Parameters.AddWithValue("@id",    idOrden);
            cmd.ExecuteNonQuery();
        }

        public void InsertarDetalleCompleto(int idOrden, string nombreManual, string item,
            string catalogo, string modelo, string descripcion, string fechaEntrega,
            decimal iva, int cantidad, string um, decimal precio, decimal descuento)
        {
            using var con = CrearConexion();
            con.Open();
            decimal subtotal = cantidad * precio * (1 - descuento / 100) * (1 + iva / 100);
            var cmd = new MySqlCommand(
                "INSERT INTO detalle_orden (id_orden, cantidad, precio, subtotal, " +
                "nombre_producto_manual, item, catalogo, modelo, descripcion, " +
                "fecha_entrega, iva, um, descuento) " +
                "VALUES (@ord,@cant,@prec,@sub,@nom,@item,@cat,@mod,@desc,@fec,@iva,@um,@dsc);", con);
            cmd.Parameters.AddWithValue("@ord",  idOrden);
            cmd.Parameters.AddWithValue("@cant", cantidad);
            cmd.Parameters.AddWithValue("@prec", precio);
            cmd.Parameters.AddWithValue("@sub",  subtotal);
            cmd.Parameters.AddWithValue("@nom",  nombreManual);
            cmd.Parameters.AddWithValue("@item", item);
            cmd.Parameters.AddWithValue("@cat",  catalogo);
            cmd.Parameters.AddWithValue("@mod",  modelo);
            cmd.Parameters.AddWithValue("@desc", descripcion);
            cmd.Parameters.AddWithValue("@fec",  fechaEntrega);
            cmd.Parameters.AddWithValue("@iva",  iva);
            cmd.Parameters.AddWithValue("@um",   um);
            cmd.Parameters.AddWithValue("@dsc",  descuento);
            cmd.ExecuteNonQuery();
        }

        public void EliminarDetalles(int idOrden)
        {
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "DELETE FROM detalle_orden WHERE id_orden = @id", con);
            cmd.Parameters.AddWithValue("@id", idOrden);
            cmd.ExecuteNonQuery();
        }

        public OrdenCompra? ObtenerOrdenPorId(int idOrden)
        {
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "SELECT id_orden, id_proveedor, total, fecha, estado, numero_orden, " +
                "IFNULL(condiciones,'30 días') AS condiciones, " +
                "IFNULL(moneda,'COP') AS moneda, " +
                "IFNULL(comprador,'') AS comprador, " +
                "IFNULL(entregar_a,'SUPLINDUSTRIA S.A.S.') AS entregar_a, " +
                "IFNULL(entregar_alterno,'NA') AS entregar_alterno, " +
                "IFNULL(observaciones,'') AS observaciones " +
                "FROM ordenes_compra WHERE id_orden = @id", con);
            cmd.Parameters.AddWithValue("@id", idOrden);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new OrdenCompra
            {
                IdOrden         = r.GetInt32("id_orden"),
                IdProveedor     = r.GetInt32("id_proveedor"),
                Total           = r.GetDecimal("total"),
                Fecha           = r.GetDateTime("fecha"),
                Estado          = r.GetString("estado"),
                NumeroOrden     = r.GetString("numero_orden"),
                Condiciones     = r.GetString("condiciones"),
                Moneda          = r.GetString("moneda"),
                Comprador       = r.GetString("comprador"),
                EntregarA       = r.GetString("entregar_a"),
                EntregarAlterno = r.GetString("entregar_alterno"),
                Observaciones   = r.GetString("observaciones")
            };
        }

        public List<DetalleOrden> ObtenerDetallesPorOrden(int idOrden)
        {
            var lista = new List<DetalleOrden>();
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "SELECT id, id_orden, cantidad, precio, subtotal, " +
                "IFNULL(nombre_producto_manual,'') AS nombre_producto_manual, " +
                "IFNULL(item,'') AS item, IFNULL(catalogo,'') AS catalogo, " +
                "IFNULL(modelo,'') AS modelo, IFNULL(descripcion,'') AS descripcion, " +
                "IFNULL(fecha_entrega,'') AS fecha_entrega, " +
                "IFNULL(iva,0) AS iva, IFNULL(um,'UND') AS um, IFNULL(descuento,0) AS descuento " +
                "FROM detalle_orden WHERE id_orden = @id ORDER BY id", con);
            cmd.Parameters.AddWithValue("@id", idOrden);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                lista.Add(new DetalleOrden
                {
                    Id                  = r.GetInt32("id"),
                    IdOrden             = r.GetInt32("id_orden"),
                    Cantidad            = r.GetInt32("cantidad"),
                    Precio              = r.GetDecimal("precio"),
                    Subtotal            = r.GetDecimal("subtotal"),
                    NombreProductoManual = r.GetString("nombre_producto_manual"),
                    Item                = r.GetString("item"),
                    Catalogo            = r.GetString("catalogo"),
                    Modelo              = r.GetString("modelo"),
                    Descripcion         = r.GetString("descripcion"),
                    FechaEntrega        = r.GetString("fecha_entrega"),
                    Iva                 = r.GetDecimal("iva"),
                    Um                  = r.GetString("um"),
                    Descuento           = r.GetDecimal("descuento")
                });
            return lista;
        }

        public List<OrdenCompra> ObtenerOrdenes()
        {
            var lista = new List<OrdenCompra>();
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "SELECT o.id_orden, o.id_proveedor, o.total, o.fecha, o.estado, " +
                "IFNULL(o.numero_orden,'') AS numero_orden, p.nombre AS nombre_proveedor " +
                "FROM ordenes_compra o " +
                "LEFT JOIN proveedores p ON p.id = o.id_proveedor " +
                "ORDER BY o.fecha DESC", con);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                lista.Add(new OrdenCompra
                {
                    IdOrden         = reader.GetInt32("id_orden"),
                    IdProveedor     = reader.GetInt32("id_proveedor"),
                    Total           = reader.GetDecimal("total"),
                    Fecha           = reader.GetDateTime("fecha"),
                    Estado          = reader.GetString("estado"),
                    NumeroOrden     = reader.GetString("numero_orden"),
                    NombreProveedor = reader["nombre_proveedor"] != DBNull.Value
                                        ? reader.GetString("nombre_proveedor") : ""
                });
            return lista;
        }

        public List<Producto> ObtenerProductos()
        {
            var lista = new List<Producto>();
            using var con = CrearConexion();
            con.Open();
            var cmd = new MySqlCommand(
                "SELECT id, nombre, precio, id_proveedor FROM productos", con);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                lista.Add(new Producto
                {
                    Id          = reader.GetInt32("id"),
                    Nombre      = reader.GetString("nombre"),
                    Precio      = reader.GetDecimal("precio"),
                    IdProveedor = reader.GetInt32("id_proveedor")
                });
            return lista;
        }
    }
}
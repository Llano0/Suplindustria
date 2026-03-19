using Microsoft.AspNetCore.Mvc;
using ModuloWeb.MANAGER;
using ModuloWeb.BROKER;
using ModuloWeb.ENTITIES;
using ModuloWeb1.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ModuloWeb1.Controllers
{
    public class OrdenCompraController : Controller
    {
        OrdenCompraManager manager = new OrdenCompraManager();
        OrdenCompraBroker  broker  = new OrdenCompraBroker();

        private string RutaPlantilla =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plantillas", "PlantillaOrdenes.xlsx");

        private string RutaCredenciales =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Credenciales", "oauth-client.json");

        // ── Helpers ──────────────────────────────────────────────────────────

        private string GenerarNumeroOrden(Proveedor proveedor, int consecutivo)
        {
            // Usar prefijo del proveedor si tiene, sino 2 primeras palabras del nombre
            string prefijo;
            if (!string.IsNullOrWhiteSpace(proveedor.Prefijo))
            {
                prefijo = proveedor.Prefijo.Trim();
            }
            else
            {
                var palabras = Regex.Split(proveedor.Nombre.Trim(), @"[\s_\-]+")
                                   .Where(p => !string.IsNullOrWhiteSpace(p))
                                   .Take(2)
                                   .Select(p => Regex.Replace(p, @"[^a-zA-Z0-9]", ""))
                                   .Where(p => !string.IsNullOrEmpty(p))
                                   .ToArray();
                prefijo = palabras.Length > 0 ? string.Join("", palabras) : "ORD";
            }
            if (prefijo.Length > 20) prefijo = prefijo.Substring(0, 20);
            return $"{prefijo}-{consecutivo}";
        }

        private byte[] GenerarExcelBytes(int idOrden, string numeroOrden,
            Proveedor proveedor, DateTime fecha, OrdenCompraViewModel model)
        {
            var cabezalDto = new OrdenExcelDto
            {
                Condiciones     = model.Condiciones    ?? "",
                Moneda          = model.Moneda         ?? "COP",
                Comprador       = model.Comprador      ?? "",
                EntregarA       = model.EntregarA      ?? "SUPLINDUSTRIA S.A.S.",
                EntregarAlterno = model.EntregarAlterno ?? "NA",
                Observaciones   = model.Observaciones  ?? ""
            };

            var detallesDto = model.Productos
                .Where(p => p.Cantidad > 0 && p.PrecioUnitario > 0)
                .Select(p => new DetalleExcelDto
                {
                    NombreManual   = p.NombreManual   ?? "",
                    Item           = p.Item           ?? "",
                    Catalogo       = p.Catalogo       ?? "",
                    Modelo         = p.Modelo         ?? "",
                    Descripcion    = p.Descripcion    ?? "",
                    FechaEntrega   = p.FechaEntrega   ?? "",
                    Iva            = p.Iva,
                    Cantidad       = p.Cantidad,
                    Um             = p.Um             ?? "UND",
                    PrecioUnitario = p.PrecioUnitario,
                    Descuento      = p.Descuento
                }).ToList();

            var excelService = new ExcelOrdenService(RutaPlantilla);
            return excelService.GenerarExcel(idOrden, numeroOrden, proveedor, fecha, cabezalDto, detallesDto);
        }

        private string SubirDrive(string pdfPath, string nombreArchivo)
        {
            try
            {
                var driveService = new GoogleDriveService(RutaCredenciales);
                return driveService.SubirPdfAsync(pdfPath, nombreArchivo)
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ViewBag.WarningDrive = $"⚠️ PDF generado pero no se pudo subir a Drive: {ex.Message}";
                return "";
            }
        }

        // ── GET: Crear ───────────────────────────────────────────────────────
        public IActionResult Crear()
        {
            ViewBag.Proveedores = broker.ObtenerProveedores();
            return View();
        }

        // ── POST: Crear ──────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult Crear([FromForm] string datosOrden)
        {
            try
            {
                var model = JsonSerializer.Deserialize<OrdenCompraViewModel>(datosOrden,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (model == null || !model.Productos.Any())
                {
                    ViewBag.Error = "⚠️ Debe agregar al menos un producto";
                    ViewBag.Proveedores = broker.ObtenerProveedores();
                    return View();
                }

                decimal total = model.Productos
                    .Where(p => p.Cantidad > 0 && p.PrecioUnitario > 0)
                    .Sum(p => p.Cantidad * p.PrecioUnitario
                              * (1 - p.Descuento / 100)
                              * (1 + p.Iva / 100));

                var proveedor = broker.ObtenerProveedorPorId(model.IdProveedor)
                    ?? new Proveedor { Nombre = "SinProveedor" };
                int consecutivo    = broker.ObtenerYIncrementarConsecutivo(model.IdProveedor);
                string numeroOrden = GenerarNumeroOrden(proveedor, consecutivo);

                // Guardar orden
                int idOrden = broker.InsertarOrden(
                    model.IdProveedor, total,
                    model.Condiciones    ?? "",
                    model.Moneda         ?? "COP",
                    model.Comprador      ?? "",
                    model.EntregarA      ?? "SUPLINDUSTRIA S.A.S.",
                    model.EntregarAlterno ?? "NA",
                    model.Observaciones  ?? "");

                broker.GuardarNumeroOrden(idOrden, numeroOrden);

                // Guardar detalles completos
                foreach (var p in model.Productos.Where(p => p.Cantidad > 0 && p.PrecioUnitario > 0))
                    broker.InsertarDetalleCompleto(idOrden,
                        p.NombreManual ?? "", p.Item ?? "", p.Catalogo ?? "",
                        p.Modelo ?? "", p.Descripcion ?? "", p.FechaEntrega ?? "",
                        p.Iva, p.Cantidad, p.Um ?? "UND", p.PrecioUnitario, p.Descuento);

                // Generar Excel y PDF
                byte[] excelBytes = GenerarExcelBytes(idOrden, numeroOrden, proveedor, DateTime.Now, model);
                string carpeta    = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ordenes");
                string xlsxPath   = Path.Combine(carpeta, $"{numeroOrden}.xlsx");
                Directory.CreateDirectory(carpeta);
                System.IO.File.WriteAllBytes(xlsxPath, excelBytes);

                string pdfPath  = PdfConverterService.ConvertirAPdf(xlsxPath);
                string driveLink = SubirDrive(pdfPath, $"{numeroOrden}.pdf");

                ViewBag.Mensaje   = $"✅ Orden {numeroOrden} creada. Total: ${total:N2}";
                ViewBag.NumOrden  = numeroOrden;
                ViewBag.DriveLink = driveLink;
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"❌ Error: {ex.Message}";
            }

            ViewBag.Proveedores = broker.ObtenerProveedores();
            return View();
        }

        // ── GET: Editar ──────────────────────────────────────────────────────
        public IActionResult Editar(int id)
        {
            var orden    = broker.ObtenerOrdenPorId(id);
            var detalles = broker.ObtenerDetallesPorOrden(id);

            if (orden == null) return NotFound();

            var model = new OrdenCompraViewModel
            {
                IdProveedor     = orden.IdProveedor,
                Condiciones     = orden.Condiciones,
                Moneda          = orden.Moneda,
                Comprador       = orden.Comprador,
                EntregarA       = orden.EntregarA,
                EntregarAlterno = orden.EntregarAlterno,
                Observaciones   = orden.Observaciones,
                Productos       = detalles.Select(d => new DetalleProductoViewModel
                {
                    NombreManual   = d.NombreProductoManual,
                    Item           = d.Item,
                    Catalogo       = d.Catalogo,
                    Modelo         = d.Modelo,
                    Descripcion    = d.Descripcion,
                    FechaEntrega   = d.FechaEntrega,
                    Iva            = d.Iva,
                    Cantidad       = d.Cantidad,
                    Um             = d.Um,
                    PrecioUnitario = d.Precio,
                    Descuento      = d.Descuento
                }).ToList()
            };

            ViewBag.Proveedores  = broker.ObtenerProveedores();
            ViewBag.IdOrden      = id;
            ViewBag.NumeroOrden  = orden.NumeroOrden;
            ViewBag.FechaOriginal = orden.Fecha;
            return View(model);
        }

        // ── POST: Editar ─────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult Editar(int id, [FromForm] string datosOrden)
        {
            try
            {
                var model = JsonSerializer.Deserialize<OrdenCompraViewModel>(datosOrden,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (model == null || !model.Productos.Any())
                {
                    ViewBag.Error = "⚠️ Debe agregar al menos un producto";
                    ViewBag.Proveedores = broker.ObtenerProveedores();
                    ViewBag.IdOrden = id;
                    return View(model);
                }

                decimal total = model.Productos
                    .Where(p => p.Cantidad > 0 && p.PrecioUnitario > 0)
                    .Sum(p => p.Cantidad * p.PrecioUnitario
                              * (1 - p.Descuento / 100)
                              * (1 + p.Iva / 100));

                // Obtener orden original para mantener numero_orden y fecha
                var ordenOriginal = broker.ObtenerOrdenPorId(id)!;
                var proveedor     = broker.ObtenerProveedorPorId(model.IdProveedor)
                    ?? new Proveedor { Nombre = "SinProveedor" };

                // Actualizar orden
                broker.ActualizarOrden(id, model.IdProveedor, total,
                    model.Condiciones    ?? "",
                    model.Moneda         ?? "COP",
                    model.Comprador      ?? "",
                    model.EntregarA      ?? "SUPLINDUSTRIA S.A.S.",
                    model.EntregarAlterno ?? "NA",
                    model.Observaciones  ?? "");

                // Reemplazar detalles
                broker.EliminarDetalles(id);
                foreach (var p in model.Productos.Where(p => p.Cantidad > 0 && p.PrecioUnitario > 0))
                    broker.InsertarDetalleCompleto(id,
                        p.NombreManual ?? "", p.Item ?? "", p.Catalogo ?? "",
                        p.Modelo ?? "", p.Descripcion ?? "", p.FechaEntrega ?? "",
                        p.Iva, p.Cantidad, p.Um ?? "UND", p.PrecioUnitario, p.Descuento);

                // Regenerar Excel y PDF con el mismo numero_orden
                string numeroOrden = ordenOriginal.NumeroOrden;
                byte[] excelBytes  = GenerarExcelBytes(id, numeroOrden, proveedor, ordenOriginal.Fecha, model);
                string carpeta     = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ordenes");
                string xlsxPath    = Path.Combine(carpeta, $"{numeroOrden}.xlsx");
                Directory.CreateDirectory(carpeta);
                System.IO.File.WriteAllBytes(xlsxPath, excelBytes);

                string pdfPath   = PdfConverterService.ConvertirAPdf(xlsxPath);
                string driveLink = SubirDrive(pdfPath, $"{numeroOrden}.pdf");

                ViewBag.Mensaje   = $"✅ Orden {numeroOrden} actualizada. Total: ${total:N2}";
                ViewBag.NumOrden  = numeroOrden;
                ViewBag.DriveLink = driveLink;
                ViewBag.IdOrden   = id;
                ViewBag.NumeroOrden = numeroOrden;
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"❌ Error: {ex.Message}";
                ViewBag.IdOrden = id;
            }

            ViewBag.Proveedores = broker.ObtenerProveedores();
            var ordenAct = broker.ObtenerOrdenPorId(id);
            var detallesAct = broker.ObtenerDetallesPorOrden(id);
            var modelReturn = new OrdenCompraViewModel
            {
                IdProveedor     = ordenAct?.IdProveedor ?? 0,
                Condiciones     = ordenAct?.Condiciones ?? "",
                Moneda          = ordenAct?.Moneda ?? "COP",
                Comprador       = ordenAct?.Comprador ?? "",
                EntregarA       = ordenAct?.EntregarA ?? "",
                EntregarAlterno = ordenAct?.EntregarAlterno ?? "NA",
                Observaciones   = ordenAct?.Observaciones ?? "",
                Productos       = detallesAct.Select(d => new DetalleProductoViewModel
                {
                    NombreManual   = d.NombreProductoManual,
                    Item           = d.Item,
                    Catalogo       = d.Catalogo,
                    Modelo         = d.Modelo,
                    Descripcion    = d.Descripcion,
                    FechaEntrega   = d.FechaEntrega,
                    Iva            = d.Iva,
                    Cantidad       = d.Cantidad,
                    Um             = d.Um,
                    PrecioUnitario = d.Precio,
                    Descuento      = d.Descuento
                }).ToList()
            };
            return View(modelReturn);
        }

        // ── POST: Crear proveedor (AJAX) ─────────────────────────────────────
        [HttpPost]
        public IActionResult CrearProveedor([FromBody] ProveedorViewModel vm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(vm.Nombre))
                    return BadRequest("El nombre es obligatorio");

                var p = new Proveedor
                {
                    Nombre    = vm.Nombre.Trim(),
                    Nit       = vm.Nit?.Trim()       ?? "",
                    Correo    = vm.Correo?.Trim()     ?? "",
                    Telefono  = vm.Telefono?.Trim()   ?? "",
                    Direccion = vm.Direccion?.Trim()  ?? "",
                    Ciudad    = vm.Ciudad?.Trim()     ?? "",
                    Contacto  = vm.Contacto?.Trim()   ?? "",
                    Prefijo   = vm.Prefijo?.Trim()    ?? ""
                };

                int newId = broker.InsertarProveedor(p);
                return Ok(new {
                    id=newId, nombre=p.Nombre, nit=p.Nit, direccion=p.Direccion,
                    telefono=p.Telefono, ciudad=p.Ciudad, contacto=p.Contacto,
                    correo=p.Correo, prefijo=p.Prefijo
                });
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── POST: Editar proveedor (AJAX) ────────────────────────────────────
        [HttpPost]
        public IActionResult EditarProveedor([FromBody] ProveedorViewModel vm)
        {
            try
            {
                if (vm.Id <= 0 || string.IsNullOrWhiteSpace(vm.Nombre))
                    return BadRequest("ID y nombre son obligatorios");

                var p = new Proveedor
                {
                    Id        = vm.Id,
                    Nombre    = vm.Nombre.Trim(),
                    Nit       = vm.Nit?.Trim()       ?? "",
                    Correo    = vm.Correo?.Trim()     ?? "",
                    Telefono  = vm.Telefono?.Trim()   ?? "",
                    Direccion = vm.Direccion?.Trim()  ?? "",
                    Ciudad    = vm.Ciudad?.Trim()     ?? "",
                    Contacto  = vm.Contacto?.Trim()   ?? "",
                    Prefijo   = vm.Prefijo?.Trim()    ?? ""
                };

                broker.ActualizarProveedor(p);
                return Ok(new {
                    id=p.Id, nombre=p.Nombre, nit=p.Nit, direccion=p.Direccion,
                    telefono=p.Telefono, ciudad=p.Ciudad, contacto=p.Contacto,
                    correo=p.Correo, prefijo=p.Prefijo
                });
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── POST: Eliminar proveedor (AJAX) ──────────────────────────────────
        [HttpPost]
        public IActionResult EliminarProveedor([FromBody] int id)
        {
            try
            {
                bool ok = broker.EliminarProveedor(id);
                if (!ok)
                    return BadRequest("No se puede eliminar: el proveedor tiene órdenes asociadas.");
                return Ok();
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        // ── GET: Lista ───────────────────────────────────────────────────────
        public IActionResult Lista()
        {
            var ordenes = broker.ObtenerOrdenes();
            return View(ordenes);
        }

        // ── GET: Descargar por número ────────────────────────────────────────
        public IActionResult DescargarPorNumero(string numero)
        {
            try
            {
                string carpeta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ordenes");
                string pdf  = Path.Combine(carpeta, $"{numero}.pdf");
                string xlsx = Path.Combine(carpeta, $"{numero}.xlsx");

                if (System.IO.File.Exists(pdf))
                    return File(System.IO.File.ReadAllBytes(pdf), "application/pdf", $"{numero}.pdf");
                if (System.IO.File.Exists(xlsx))
                    return File(System.IO.File.ReadAllBytes(xlsx),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        $"{numero}.xlsx");

                return NotFound("Archivo no encontrado.");
            }
            catch (Exception ex) { return BadRequest($"Error: {ex.Message}"); }
        }
    }
}
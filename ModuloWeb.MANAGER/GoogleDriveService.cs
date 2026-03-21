using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;

namespace ModuloWeb.MANAGER
{
    /// <summary>
    /// Sube archivos a Google Drive usando cuenta de servicio — nunca expira.
    /// </summary>
    public class GoogleDriveService
    {
        private const string FOLDER_ID = "1aTN1zSyKDh2_9f37Ytx8mpBX0mWs8qgS";
        private static readonly string[] Scopes = { DriveService.ScopeConstants.Drive };

        private readonly string _rutaCredenciales;

        public GoogleDriveService(string rutaCredenciales)
        {
            _rutaCredenciales = rutaCredenciales;
        }

        public async Task<string> SubirPdfAsync(string rutaPdf, string nombreArchivo)
        {
            // Leer JSON desde variable de entorno (Railway) o archivo local
            string? jsonContent = Environment.GetEnvironmentVariable("GOOGLE_SERVICE_ACCOUNT_JSON");

            GoogleCredential credential;

            if (!string.IsNullOrWhiteSpace(jsonContent))
            {
                using var stream = new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes(jsonContent));
                credential = GoogleCredential
                    .FromStream(stream)
                    .CreateScoped(Scopes);
            }
            else if (File.Exists(_rutaCredenciales))
            {
                using var stream = new FileStream(
                    _rutaCredenciales, FileMode.Open, FileAccess.Read);
                credential = GoogleCredential
                    .FromStream(stream)
                    .CreateScoped(Scopes);
            }
            else
            {
                throw new FileNotFoundException(
                    $"Credenciales no encontradas. Configura GOOGLE_SERVICE_ACCOUNT_JSON en Railway.");
            }

            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName       = "OrdenesCompra"
            });

            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name    = nombreArchivo,
                Parents = new[] { FOLDER_ID }
            };

            using var contenido = new FileStream(rutaPdf, FileMode.Open, FileAccess.Read);
            var request = service.Files.Create(metadata, contenido, "application/pdf");
            request.Fields = "id, name, webViewLink";

            var resultado = await request.UploadAsync();
            if (resultado.Status != UploadStatus.Completed)
                throw new Exception($"Error subiendo a Drive: {resultado.Exception?.Message}");

            return request.ResponseBody?.WebViewLink ?? "";
        }
    }
}
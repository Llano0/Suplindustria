using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;

namespace ModuloWeb.MANAGER
{
    public class GoogleDriveService
    {
        private const string FOLDER_ID = "1aTN1zSyKDh2_9f37Ytx8mpBX0mWs8qgS";
        private static readonly string[] Scopes = { DriveService.ScopeConstants.DriveFile };

        private readonly string _rutaCredenciales;

        public GoogleDriveService(string rutaCredenciales)
        {
            _rutaCredenciales = rutaCredenciales;
        }

        public async Task<string> SubirPdfAsync(string rutaPdf, string nombreArchivo)
        {
            UserCredential credential;

            // Intentar leer desde variable de entorno (Railway/producción)
            string? jsonEnv = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_JSON");

            if (!string.IsNullOrWhiteSpace(jsonEnv))
            {
                // Usar el JSON de la variable de entorno
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonEnv));
                string tokenPath = Path.Combine(Path.GetTempPath(), "token_drive");
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(tokenPath, true)
                );
            }
            else if (File.Exists(_rutaCredenciales))
            {
                // Usar archivo local (desarrollo en Windows)
                using var stream = new FileStream(_rutaCredenciales, FileMode.Open, FileAccess.Read);
                string tokenPath = Path.Combine(
                    Path.GetDirectoryName(_rutaCredenciales)!, "token_drive");
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(tokenPath, true)
                );
            }
            else
            {
                throw new FileNotFoundException(
                    $"Credenciales OAuth no encontradas en: {_rutaCredenciales}\n" +
                    "Configura la variable GOOGLE_OAUTH_JSON en Railway.");
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
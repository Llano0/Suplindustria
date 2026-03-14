using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Newtonsoft.Json;

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

            // Leer client_id y client_secret del JSON de credenciales
            string oauthJson = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_JSON")
                ?? (File.Exists(_rutaCredenciales) ? File.ReadAllText(_rutaCredenciales) : null)
                ?? throw new Exception("No se encontró GOOGLE_OAUTH_JSON ni el archivo de credenciales.");

            // Leer el token guardado
            string? tokenJson = Environment.GetEnvironmentVariable("GOOGLE_TOKEN_JSON");
            if (string.IsNullOrWhiteSpace(tokenJson))
            {
                // Buscar token en archivo local
                string tokenPath = Path.Combine(
                    Path.GetDirectoryName(_rutaCredenciales)!,
                    "token_drive",
                    "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user");
                if (File.Exists(tokenPath))
                    tokenJson = File.ReadAllText(tokenPath);
            }

            if (string.IsNullOrWhiteSpace(tokenJson))
{
    // No hay token guardado → abrir navegador para autorizar (solo funciona local)
    string tokenDir = Path.Combine(
        Path.GetDirectoryName(_rutaCredenciales)!, "token_drive");
    using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(oauthJson));
    var cred = await GoogleWebAuthorizationBroker.AuthorizeAsync(
        GoogleClientSecrets.FromStream(stream).Secrets,
        Scopes,
        "user",
        CancellationToken.None,
        new Google.Apis.Util.Store.FileDataStore(tokenDir, true)
    );
    // Leer el token recién guardado
    string tokenFile = Path.Combine(tokenDir,
        "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user");
    tokenJson = File.ReadAllText(tokenFile);
}

            // Extraer client_id y client_secret del oauth JSON
            dynamic oauthData = JsonConvert.DeserializeObject(oauthJson)!;
            string clientId     = (string)oauthData.installed.client_id;
            string clientSecret = (string)oauthData.installed.client_secret;

            // Crear credencial desde el token guardado (sin abrir navegador)
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(tokenJson)!;

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId     = clientId,
                    ClientSecret = clientSecret
                },
                Scopes = Scopes
            });

            credential = new UserCredential(flow, "user", tokenResponse);

            // Refrescar el token si expiró
            await credential.RefreshTokenAsync(CancellationToken.None);

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
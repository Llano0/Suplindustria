using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using System.Text;

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
            string oauthJson = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_JSON")
                ?? (File.Exists(_rutaCredenciales) ? File.ReadAllText(_rutaCredenciales) : null)
                ?? throw new Exception("No se encontró GOOGLE_OAUTH_JSON.");

            string? tokenJson = Environment.GetEnvironmentVariable("GOOGLE_TOKEN_JSON");
            if (string.IsNullOrWhiteSpace(tokenJson))
            {
                string tokenPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(_rutaCredenciales)!,
                    "token_drive",
                    "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user");
                if (File.Exists(tokenPath))
                    tokenJson = File.ReadAllText(tokenPath);
            }

            UserCredential credential;

            if (string.IsNullOrWhiteSpace(tokenJson))
            {
                // Sin token → abrir navegador para autorizar
                string tokenDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(_rutaCredenciales)!, "token_drive");
                using var streamAuth = new MemoryStream(
                    Encoding.UTF8.GetBytes(oauthJson));
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(streamAuth).Secrets,
                    Scopes, "user", CancellationToken.None,
                    new FileDataStore(tokenDir, true));
            }
            else
            {
                dynamic oauthData = JsonConvert.DeserializeObject(oauthJson)!;
                string clientId     = (string)oauthData.installed.client_id;
                string clientSecret = (string)oauthData.installed.client_secret;

                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(tokenJson)!;
                var flow = new GoogleAuthorizationCodeFlow(
                    new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecrets = new ClientSecrets
                        {
                            ClientId     = clientId,
                            ClientSecret = clientSecret
                        },
                        Scopes = Scopes
                    });

                credential = new UserCredential(flow, "user", tokenResponse);
                await credential.RefreshTokenAsync(CancellationToken.None);

                // Guardar token actualizado en Railway
                await GuardarTokenEnRailway(credential.Token);
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

        private static async Task GuardarTokenEnRailway(TokenResponse token)
        {
            try
            {
                string? railwayToken  = Environment.GetEnvironmentVariable("RAILWAY_TOKEN");
                string? serviceId     = Environment.GetEnvironmentVariable("RAILWAY_SERVICE_ID");
                string? environmentId = Environment.GetEnvironmentVariable("RAILWAY_ENVIRONMENT_ID");
                string? projectId     = Environment.GetEnvironmentVariable("RAILWAY_PROJECT_ID");

                if (string.IsNullOrEmpty(railwayToken)) return;

                string tokenJson = JsonConvert.SerializeObject(token);
                var query = new
                {
                    query = @"mutation($input: VariableUpsertInput!) {
                        variableUpsert(input: $input)
                    }",
                    variables = new
                    {
                        input = new
                        {
                            projectId,
                            environmentId,
                            serviceId,
                            name  = "GOOGLE_TOKEN_JSON",
                            value = tokenJson
                        }
                    }
                };

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {railwayToken}");
                var content = new StringContent(
                    JsonConvert.SerializeObject(query),
                    Encoding.UTF8, "application/json");
                await http.PostAsync("https://backboard.railway.app/graphql/v2", content);
            }
            catch { }
        }
    }
}
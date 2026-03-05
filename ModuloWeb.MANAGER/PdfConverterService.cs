using System.Diagnostics;

namespace ModuloWeb.MANAGER
{
    public class PdfConverterService
    {
        private static readonly string[] _rutasCandidatas = new[]
        {
            // Linux (Docker/Railway)
            "/usr/bin/soffice",
            "/usr/lib/libreoffice/program/soffice",
            "/opt/libreoffice/program/soffice",
            // Windows
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
            @"C:\LibreOffice\program\soffice.exe",
        };

        public static string? EncontrarLibreOffice()
        {
            foreach (var ruta in _rutasCandidatas)
                if (File.Exists(ruta)) return ruta;
            return null;
        }

        public static string ConvertirAPdf(string xlsxPath)
        {
            string? soffice = EncontrarLibreOffice();
            if (soffice == null)
                throw new InvalidOperationException(
                    "LibreOffice no encontrado. Instálalo desde https://www.libreoffice.org/download/download/ " +
                    "y vuelve a intentarlo.");

            string carpeta = Path.GetDirectoryName(xlsxPath)!;

            var psi = new ProcessStartInfo
            {
                FileName               = soffice,
                Arguments              = $"--headless --norestore --convert-to pdf \"{xlsxPath}\" --outdir \"{carpeta}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            // Variables de entorno necesarias en Linux
            psi.Environment["HOME"]   = "/tmp";
            psi.Environment["TMPDIR"] = "/tmp";

            using var proc = Process.Start(psi)
                ?? throw new Exception("No se pudo iniciar LibreOffice.");

            proc.WaitForExit(120_000);

            string pdfPath = Path.ChangeExtension(xlsxPath, ".pdf");
            if (!File.Exists(pdfPath))
                throw new Exception(
                    $"LibreOffice no generó el PDF. Error: {proc.StandardError.ReadToEnd()}");

            return pdfPath;
        }
    }
}
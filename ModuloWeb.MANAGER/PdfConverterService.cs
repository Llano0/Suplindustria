using System.Diagnostics;

namespace ModuloWeb.MANAGER
{
    public class PdfConverterService
    {
        private static readonly string[] _rutasCandidatas = new[]
        {
            "/usr/bin/soffice",
            "/usr/lib/libreoffice/program/soffice",
            "/opt/libreoffice/program/soffice",
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
            @"C:\LibreOffice\program\soffice.exe",
        };

        private static readonly string[] _gsRutas = new[]
        {
            "gs",                    // Linux
            "gswin64c",              // Windows PATH
            @"C:\Program Files\gs\gs10.03.1\bin\gswin64c.exe",
            @"C:\Program Files\gs\gs10.03.0\bin\gswin64c.exe",
            @"C:\Program Files\gs\gs10.02.1\bin\gswin64c.exe",
            @"C:\Program Files\gs\gs10.01.2\bin\gswin64c.exe",
        };

        public static string? EncontrarLibreOffice()
        {
            foreach (var ruta in _rutasCandidatas)
                if (File.Exists(ruta)) return ruta;
            return null;
        }

        private static string? EncontrarGhostscript()
        {
            foreach (var r in _gsRutas)
            {
                try
                {
                    var p = Process.Start(new ProcessStartInfo
                    {
                        FileName = r, Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute = false,
                        CreateNoWindow  = true
                    });
                    p?.WaitForExit(3000);
                    if (p?.ExitCode == 0) return r;
                }
                catch { }
            }
            return null;
        }

        public static string ConvertirAPdf(string xlsxPath)
        {
            string? soffice = EncontrarLibreOffice();
            if (soffice == null)
                throw new InvalidOperationException(
                    "LibreOffice no encontrado. Instálalo desde https://www.libreoffice.org/download/download/");

            string carpeta = Path.GetDirectoryName(xlsxPath)!;
            string pdfPath = Path.ChangeExtension(xlsxPath, ".pdf");

            // Paso 1: LibreOffice → PDF
            var psi = new ProcessStartInfo
            {
                FileName               = soffice,
                Arguments              = $"--headless --norestore --convert-to pdf \"{xlsxPath}\" --outdir \"{carpeta}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            psi.Environment["HOME"]   = "/tmp";
            psi.Environment["TMPDIR"] = "/tmp";

            using (var proc = Process.Start(psi) ?? throw new Exception("No se pudo iniciar LibreOffice."))
                proc.WaitForExit(120_000);

            if (!File.Exists(pdfPath))
                throw new Exception("LibreOffice no generó el PDF.");

            // Paso 2: Ghostscript → aplica márgenes de 1cm
            string? gs = EncontrarGhostscript();
            if (gs != null)
            {
                string pdfTemp = pdfPath + ".tmp.pdf";
                // A4 landscape: 842 x 595 puntos
                // margen 1cm = 28.35 pts → escalar contenido al 93.27% y centrarlo
                var gsArgs =
                    $"-dBATCH -dNOPAUSE -sDEVICE=pdfwrite " +
                    $"-dFIXEDMEDIA " +
                    $"-dDEVICEWIDTHPOINTS=842 -dDEVICEHEIGHTPOINTS=595 " +
                    $"-dPDFFitPage " +
                    $"\"-sOutputFile={pdfTemp}\" \"{pdfPath}\"";

                var gsPsi = new ProcessStartInfo
                {
                    FileName               = gs,
                    Arguments              = gsArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using (var gsproc = Process.Start(gsPsi))
                    gsproc?.WaitForExit(30_000);

                if (File.Exists(pdfTemp))
                {
                    File.Delete(pdfPath);
                    File.Move(pdfTemp, pdfPath);
                }
            }

            return pdfPath;
        }
    }
}
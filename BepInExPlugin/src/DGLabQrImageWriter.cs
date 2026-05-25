using System.IO;
using QRCoder;

namespace DGLab.BepInEx
{
    internal static class DGLabQrImageWriter
    {
        public static string WritePng(string directory, string url)
        {
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(url)) return string.Empty;

            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "dglab-qr.png");
            using (var generator = new QRCodeGenerator())
            using (var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M))
            {
                var qr = new PngByteQRCode(data);
                File.WriteAllBytes(path, qr.GetGraphic(12));
            }

            return path;
        }
    }
}

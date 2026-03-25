using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text;

namespace SvgTracerMac;

/*  Utility statiche per elaborazione immagini e generazione SVG  */
public static class ImageTools {

    #region RECORD

    public record ImageMetadata(int Width, int Height, double DpiX, double DpiY);

    public record TraceResult(ContourDetector.TracePoint[][] Contours, int Width, int Height);

    #endregion

    #region METODI PUBBLICI

    /*  Legge i metadati dell'immagine usando ImageSharp  */
    public static Task<ImageMetadata> ReadMetadataAsync(string file_path) {
        return Task.Run(() => {
            using var image = SixLabors.ImageSharp.Image.Load(file_path);
            return new ImageMetadata(
                image.Width,
                image.Height,
                image.Metadata.HorizontalResolution,
                image.Metadata.VerticalResolution

            );

        });

    }

    /*  Esegue il tracciamento dei contorni con soglia configurabile  */
    public static Task<TraceResult> TraceContoursAsync(string input_path, int threshold, int min_area) {
        return Task.Run(() => {
            var result = ContourDetector.Detect(input_path, threshold, min_area);
            return new TraceResult(result.Contours, result.Width, result.Height);

        });

    }

    /*  Genera il contenuto SVG con clipPath per sfondo trasparente  */
    public static string GenerateSvg(ContourDetector.TracePoint[][] contours, int w, int h, string img_path) {
        var sb = new StringBuilder();
        string b64 = Convert.ToBase64String(File.ReadAllBytes(img_path));
        string mime_type = GetMimeType(img_path);

        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"{w}\" height=\"{h}\">");
        sb.AppendLine("  <defs>");
        sb.AppendLine("    <clipPath id=\"subject-clip\">");

        foreach (var c in contours) {
            if (c.Length < 2) continue;

            var pts = string.Join(" L ", c.Select(p => $"{p.X} {p.Y}"));
            sb.AppendLine($"      <path d=\"M {pts} Z\" />");

        }

        sb.AppendLine("    </clipPath>");
        sb.AppendLine("  </defs>");
        sb.AppendLine($"  <g clip-path=\"url(#subject-clip)\">");
        sb.AppendLine($"    <image xlink:href=\"data:{mime_type};base64,{b64}\" width=\"{w}\" height=\"{h}\" />");
        sb.AppendLine("  </g>");
        sb.AppendLine("</svg>");

        return sb.ToString();

    }

    /*  Renderizza l'immagine con i contorni sovrapposti e salva come PNG per l'anteprima  */
    public static Task RenderPreviewAsync(
        string background_path,
        ContourDetector.TracePoint[][] contours,
        string output_path) {
        return Task.Run(() => {
            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(background_path);

            var stroke_color = SixLabors.ImageSharp.Color.FromRgba(255, 0, 255, 200);

            foreach (var contour in contours) {
                if (contour.Length < 2) continue;

                var points = contour
                    .Select(p => new SixLabors.ImageSharp.PointF(p.X, p.Y))
                    .ToArray();

                image.Mutate(ctx => {
                    ctx.DrawPolygon(stroke_color, 2f, points);

                });

            }

            using var file_stream = File.Create(output_path);
            image.Save(file_stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());

        });

    }

    /*  Determina il MIME type in base all'estensione del file  */
    public static string GetMimeType(string file_path) {
        var ext = Path.GetExtension(file_path).ToLowerInvariant();
        return ext switch {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".tiff" or ".tif" => "image/tiff",
            ".webp" => "image/webp",
            ".tga" => "image/x-tga",
            ".pbm" => "image/x-portable-bitmap",
            _ => "application/octet-stream",

        };

    }

    #endregion

}

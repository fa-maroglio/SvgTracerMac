using SixLabors.ImageSharp.PixelFormats;

namespace SvgTracerMac;

/*  Rilevamento contorni gestito senza dipendenze native (sostituisce OpenCvSharp)  */
public static class ContourDetector {

    #region TIPI

    public readonly record struct TracePoint(int X, int Y);

    #endregion

    #region CAMPI PRIVATI

    /*  8 direzioni in senso orario: E, SE, S, SW, W, NW, N, NE  */
    private static readonly int[] _dx = [1, 1, 0, -1, -1, -1, 0, 1];
    private static readonly int[] _dy = [0, 1, 1, 1, 0, -1, -1, -1];

    #endregion

    #region METODI PUBBLICI

    /*  Crea una maschera binaria e rileva i contorni esterni dell'immagine  */
    public static (TracePoint[][] Contours, int Width, int Height) Detect(
        string image_path, int threshold, int min_area) {

        using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(image_path);
        int w = image.Width;
        int h = image.Height;

        /*  Maschera binaria: true = primo piano (non-bianco e non-trasparente)  */
        var mask = new bool[h, w];
        image.ProcessPixelRows(accessor => {
            for (int y = 0; y < h; y++) {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < w; x++) {
                    var px = row[x];
                    bool is_white = px.R >= threshold && px.G >= threshold && px.B >= threshold;
                    mask[y, x] = !is_white && px.A > 0;

                }

            }

        });

        var contours = FindExternalContours(mask, w, h, min_area);
        return (contours, w, h);

    }

    #endregion

    #region METODI PRIVATI

    /*  Scansiona la maschera e rileva tutti i contorni esterni  */
    private static TracePoint[][] FindExternalContours(bool[,] mask, int width, int height, int min_area) {
        var labels = new int[height, width];
        var contours = new List<TracePoint[]>();
        int label = 0;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                if (!mask[y, x] || labels[y, x] != 0) continue;

                label++;
                var boundary = TraceMooreBoundary(mask, x, y, width, height);
                FloodFill(mask, labels, x, y, width, height, label);

                if (boundary.Length < 2) continue;

                double area = CalculateArea(boundary);
                if (area >= min_area) {
                    contours.Add(SimplifyContour(boundary));

                }

            }

        }

        return contours.ToArray();

    }

    /*  Traccia il contorno usando l'algoritmo di Moore (inseguimento del bordo)  */
    private static TracePoint[] TraceMooreBoundary(bool[,] mask, int start_x, int start_y, int width, int height) {
        var boundary = new List<TracePoint> { new(start_x, start_y) };

        /*  Direzione iniziale: W (ovest) perché la scansione arriva da sinistra  */
        int backtrack = 4;
        int cx = start_x, cy = start_y;
        int max_steps = width * height;

        for (int step = 0; step < max_steps; step++) {
            int search_start = (backtrack + 1) % 8;
            int found_dir = -1;

            for (int i = 0; i < 8; i++) {
                int dir = (search_start + i) % 8;
                int nx = cx + _dx[dir];
                int ny = cy + _dy[dir];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height && mask[ny, nx]) {
                    found_dir = dir;
                    break;

                }

            }

            if (found_dir == -1) break;

            int next_x = cx + _dx[found_dir];
            int next_y = cy + _dy[found_dir];

            /*  Condizione di arresto: ritorno al punto iniziale  */
            if (next_x == start_x && next_y == start_y && step > 0) break;

            cx = next_x;
            cy = next_y;
            backtrack = (found_dir + 4) % 8;

            boundary.Add(new TracePoint(cx, cy));

        }

        return boundary.ToArray();

    }

    /*  Flood fill iterativo per etichettare i pixel di un componente connesso  */
    private static void FloodFill(bool[,] mask, int[,] labels, int start_x, int start_y, int width, int height, int label) {
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((start_x, start_y));
        labels[start_y, start_x] = label;

        while (queue.Count > 0) {
            var (x, y) = queue.Dequeue();

            for (int d = 0; d < 8; d++) {
                int nx = x + _dx[d];
                int ny = y + _dy[d];

                if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                    mask[ny, nx] && labels[ny, nx] == 0) {
                    labels[ny, nx] = label;
                    queue.Enqueue((nx, ny));

                }

            }

        }

    }

    /*  Calcola l'area del contorno con la formula di Gauss (shoelace)  */
    private static double CalculateArea(TracePoint[] contour) {
        double area = 0;
        int n = contour.Length;

        for (int i = 0; i < n; i++) {
            int j = (i + 1) % n;
            area += (double)contour[i].X * contour[j].Y;
            area -= (double)contour[j].X * contour[i].Y;

        }

        return Math.Abs(area) / 2.0;

    }

    /*  Semplifica il contorno rimuovendo i punti collineari (equivalente di ApproxSimple)  */
    private static TracePoint[] SimplifyContour(TracePoint[] contour) {
        if (contour.Length <= 2) return contour;

        var simplified = new List<TracePoint> { contour[0] };

        for (int i = 1; i < contour.Length - 1; i++) {
            int dx1 = contour[i].X - contour[i - 1].X;
            int dy1 = contour[i].Y - contour[i - 1].Y;
            int dx2 = contour[i + 1].X - contour[i].X;
            int dy2 = contour[i + 1].Y - contour[i].Y;

            if (dx1 != dx2 || dy1 != dy2) {
                simplified.Add(contour[i]);

            }

        }

        simplified.Add(contour[^1]);
        return simplified.ToArray();

    }

    #endregion

}

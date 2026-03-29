
using CommunityToolkit.Maui.Storage;
using System.Text;

namespace SvgTracerMac;

public partial class MainPage : ContentPage {

    #region CAMPI PRIVATI

    private int _image_width;
    private int _image_height;
    private double _image_dpi_x;
    private double _image_dpi_y;
    private string? _svg_content;
    private string? _loaded_file_path;
    private string? _processed_temp_path;
    private string? _preview_temp_path;
    private ContourDetector.TracePoint[][]? _traced_contours;
    private ContourDetector.TracePoint[][]? _traced_holes;
    private readonly GeminiClient _gemini_client;
    private string _selected_intensity = "medio";
    private string _selected_prompt = "normale";
    private int _original_width;
    private int _original_height;

    /*  Configurazioni tracciamento: soglia bianco e area minima contorno  */
    private record TraceConfig(int Threshold, int MinArea);

    private static readonly Dictionary<string, TraceConfig> _trace_configs = new() {
        ["leggero"] = new TraceConfig(250, 50),
        ["medio"] = new TraceConfig(235, 200),
        ["forte"] = new TraceConfig(215, 500),

    };

    #endregion

    public MainPage() {
        InitializeComponent();

        string api_key = SecretsLoader.GetGeminiApiKey();
        _gemini_client = new GeminiClient(api_key);

    }

    #region METODI PUBBLICI

    /*  Gestisce il click sul bottone di caricamento immagine  */
    private async void OnLoadImageClicked(object sender, EventArgs e) {
        try {
            var file_types = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>> {
                    { DevicePlatform.MacCatalyst, new[] {
                        "public.png", "public.jpeg", "com.microsoft.bmp",
                        "com.compuserve.gif", "public.tiff", "org.webmproject.webp"

                    }},
                    { DevicePlatform.iOS, new[] {
                        "public.png", "public.jpeg", "com.microsoft.bmp",
                        "com.compuserve.gif", "public.tiff", "org.webmproject.webp"

                    }},
                    { DevicePlatform.WinUI, new[] {
                        ".png", ".jpg", ".jpeg", ".bmp", ".gif",
                        ".tga", ".tiff", ".tif", ".webp", ".pbm"

                    }},
                    { DevicePlatform.Android, new[] {
                        "image/png", "image/jpeg", "image/bmp",
                        "image/gif", "image/tiff", "image/webp"

                    }},

                }

            );

            var pick_options = new PickOptions {
                PickerTitle = "Seleziona un file immagine",
                FileTypes = file_types,

            };

            var result = await FilePicker.Default.PickAsync(pick_options);
            if (result == null) return;

            _loaded_file_path = result.FullPath;
            CleanupTempFiles();
            ControlsPanel.IsVisible = false;

            /*  Step 1/4: lettura metadati  */
            UpdateStatus("Lettura metadati immagine...", "1 / 4", true);
            LoadButton.IsEnabled = false;
            SaveButton.IsEnabled = false;

            var metadata = await ImageTools.ReadMetadataAsync(_loaded_file_path);
            _image_width = metadata.Width;
            _image_height = metadata.Height;
            _image_dpi_x = metadata.DpiX;
            _image_dpi_y = metadata.DpiY;
            _original_width = metadata.Width;
            _original_height = metadata.Height;

            UpdateInfoLabel(_image_width, _image_height);
            InfoLabel.IsVisible = true;

            /*  Step 2/4: mostra immagine originale  */
            UpdateStatus("Anteprima immagine originale...", "2 / 4", true);

            ImagePreview.Source = ImageSource.FromFile(_loaded_file_path);
            ImagePreview.IsVisible = true;
            PlaceholderPanel.IsVisible = false;

            await Task.Delay(600);

            /*  Step 3/4: rimozione sfondo con Gemini (Nano Banana)  */
            UpdateStatus("Rimozione sfondo con AI...", "3 / 4", true);

            byte[] processed_bytes = await _gemini_client.RemoveBackgroundAsync(
                _loaded_file_path, _selected_prompt

            );

            _processed_temp_path = Path.Combine(
                Path.GetTempPath(), $"svgtracer_{Guid.NewGuid():N}.png"

            );

            await File.WriteAllBytesAsync(_processed_temp_path, processed_bytes);

            ImagePreview.Source = ImageSource.FromFile(_processed_temp_path);

            await Task.Delay(400);

            /*  Step 4/4: tracciamento contorni (riutilizzabile dal selettore intensità)  */
            await RetraceWithIntensity(_selected_intensity);

            ControlsPanel.IsVisible = true;
            LoadButton.IsEnabled = true;

        }
        catch (Exception ex) {
            UpdateStatus("Errore", "", false);
            LoadButton.IsEnabled = true;

            await DisplayAlert("Errore",
                ex.Message, "OK"

            );

        }

    }

    /*  Gestisce il click sul bottone di salvataggio SVG  */
    private async void OnSaveClicked(object sender, EventArgs e) {
        try {
            if (string.IsNullOrEmpty(_svg_content)) {
                throw new InvalidOperationException(
                    "No SVG content available for saving."

                );

            }

            using var stream = new MemoryStream(
                Encoding.UTF8.GetBytes(_svg_content)

            );

            var file_result = await FileSaver.Default.SaveAsync(
                "Traccia.svg", stream, CancellationToken.None

            );

            if (file_result.IsSuccessful) {
                UpdateStatus($"Salvato: {file_result.FilePath}", "", false);

            }
            else {
                UpdateStatus("Salvataggio annullato.", "", false);

            }

        }
        catch (Exception ex) {
            await DisplayAlert("Errore",
                ex.Message, "OK"

            );

        }

    }

    /*  Gestisce il cambio di intensità del tracciamento: ri-esegue step 4  */
    private async void OnIntensityClicked(object sender, EventArgs e) {
        try {
            if (sender is not Button btn) return;
            if (string.IsNullOrEmpty(_processed_temp_path)) return;

            string intensity_key = btn.ClassId;
            if (intensity_key == _selected_intensity) return;

            _selected_intensity = intensity_key;
            UpdateIntensityButtons(intensity_key);

            await RetraceWithIntensity(intensity_key);

        }
        catch (Exception ex) {
            UpdateStatus("Errore", "", false);

            await DisplayAlert("Errore",
                ex.Message, "OK"

            );

        }

    }

    /*  Gestisce il cambio di prompt: ri-esegue step 3 (Gemini) + step 4 (tracciamento)  */
    private async void OnPromptClicked(object sender, EventArgs e) {
        try {
            if (sender is not Button btn) return;
            if (string.IsNullOrEmpty(_loaded_file_path)) return;

            string prompt_key = btn.ClassId;
            if (prompt_key == _selected_prompt) return;

            _selected_prompt = prompt_key;
            UpdatePromptButtons(prompt_key);

            await ReprocessWithPrompt(prompt_key);

        }
        catch (Exception ex) {
            UpdateStatus("Errore", "", false);

            await DisplayAlert("Errore",
                ex.Message, "OK"

            );

        }

    }

    #endregion

    #region METODI PRIVATI

    /*  Ri-esegue gli step 3-4 con il prompt selezionato  */
    private async Task ReprocessWithPrompt(string prompt_key) {
        if (string.IsNullOrEmpty(_loaded_file_path)) return;

        /*  Step 3/4: rimozione sfondo con nuovo prompt  */
        UpdateStatus("Rimozione sfondo con AI...", "3 / 4", true);
        SaveButton.IsEnabled = false;
        LoadButton.IsEnabled = false;

        CleanupTempFiles();

        byte[] processed_bytes = await _gemini_client.RemoveBackgroundAsync(
            _loaded_file_path, prompt_key

        );

        _processed_temp_path = Path.Combine(
            Path.GetTempPath(), $"svgtracer_{Guid.NewGuid():N}.png"

        );

        await File.WriteAllBytesAsync(_processed_temp_path, processed_bytes);

        ImagePreview.Source = ImageSource.FromFile(_processed_temp_path);

        await Task.Delay(400);

        /*  Step 4/4: ri-tracciamento con intensità corrente  */
        await RetraceWithIntensity(_selected_intensity);

    }

    /*  Aggiorna lo stile dei bottoni prompt evidenziando quello selezionato  */
    private void UpdatePromptButtons(string selected_key) {
        var active_bg = Color.FromArgb("#007AFF");
        var inactive_bg = Color.FromArgb("#CCCCCC");
        var active_text = Colors.White;
        var inactive_text = Color.FromArgb("#333333");

        BtnNormale.BackgroundColor = selected_key == "normale" ? active_bg : inactive_bg;
        BtnNormale.TextColor = selected_key == "normale" ? active_text : inactive_text;

        BtnAggressivo.BackgroundColor = selected_key == "aggressivo" ? active_bg : inactive_bg;
        BtnAggressivo.TextColor = selected_key == "aggressivo" ? active_text : inactive_text;

        BtnTecnico.BackgroundColor = selected_key == "tecnico" ? active_bg : inactive_bg;
        BtnTecnico.TextColor = selected_key == "tecnico" ? active_text : inactive_text;

    }


    /*  Ri-esegue lo step 4 con la configurazione di intensità selezionata  */
    private async Task RetraceWithIntensity(string intensity_key) {
        if (string.IsNullOrEmpty(_processed_temp_path)) return;

        var config = _trace_configs[intensity_key];

        /*  Step 4/4: tracciamento contorni con soglia configurata + anteprima  */
        UpdateStatus("Tracciamento contorni...", "4 / 4", true);
        SaveButton.IsEnabled = false;
        LoadButton.IsEnabled = false;

        CleanupSingleFile(ref _preview_temp_path);

        var trace_result = await ImageTools.TraceContoursAsync(
            _processed_temp_path, config.Threshold, config.MinArea

        );

        _traced_contours = trace_result.Contours;
        _traced_holes = trace_result.Holes;
        _image_width = trace_result.Width;
        _image_height = trace_result.Height;
        UpdateInfoLabel(_image_width, _image_height);

        _svg_content = ImageTools.GenerateSvg(
            _traced_contours, _traced_holes, _image_width, _image_height, _processed_temp_path

        );

        _preview_temp_path = Path.Combine(
            Path.GetTempPath(), $"svgtracer_preview_{Guid.NewGuid():N}.png"

        );

        await ImageTools.RenderPreviewAsync(
            _processed_temp_path, _traced_contours, _traced_holes, _preview_temp_path

        );

        ImagePreview.Source = ImageSource.FromFile(_preview_temp_path);

        UpdateStatus("Completato! Pronto per il salvataggio.", "4 / 4", false);
        SaveButton.IsEnabled = true;
        LoadButton.IsEnabled = true;

    }

    /*  Aggiorna lo stile dei bottoni intensità evidenziando quello selezionato  */
    private void UpdateIntensityButtons(string selected_key) {
        var active_bg = Color.FromArgb("#007AFF");
        var inactive_bg = Color.FromArgb("#CCCCCC");
        var active_text = Colors.White;
        var inactive_text = Color.FromArgb("#333333");

        BtnLeggero.BackgroundColor = selected_key == "leggero" ? active_bg : inactive_bg;
        BtnLeggero.TextColor = selected_key == "leggero" ? active_text : inactive_text;

        BtnMedio.BackgroundColor = selected_key == "medio" ? active_bg : inactive_bg;
        BtnMedio.TextColor = selected_key == "medio" ? active_text : inactive_text;

        BtnForte.BackgroundColor = selected_key == "forte" ? active_bg : inactive_bg;
        BtnForte.TextColor = selected_key == "forte" ? active_text : inactive_text;

    }

    /*  Aggiorna il pannello di stato con messaggio, step e spinner  */
    private void UpdateStatus(string message, string step, bool is_busy) {
        StatusPanel.IsVisible = true;
        StatusLabel.Text = message;
        StepLabel.Text = step;
        ProcessingIndicator.IsRunning = is_busy;

    }

    /*  Aggiorna la label informativa con le dimensioni correnti e una freccia colorata se la risoluzione è cambiata  */
    private void UpdateInfoLabel(int width, int height) {
        long orig_pixels = (long)_original_width * _original_height;
        long new_pixels = (long)width * height;
        string size_text = $"{width} \u00d7 {height} px  |  {_image_dpi_x:F0} \u00d7 {_image_dpi_y:F0} DPI";

        if (orig_pixels == 0 || orig_pixels == new_pixels) {
            var fs_plain = new FormattedString();
            fs_plain.Spans.Add(new Span { Text = size_text, TextColor = Color.FromArgb("#999999"), FontSize = 12 });
            InfoLabel.FormattedText = fs_plain;
            return;

        }

        bool increased = new_pixels > orig_pixels;
        var fs = new FormattedString();
        fs.Spans.Add(new Span { Text = size_text, TextColor = Color.FromArgb("#999999"), FontSize = 12 });
        fs.Spans.Add(new Span {
            Text = increased ? "  ▲" : "  ▼",
            TextColor = increased ? Color.FromArgb("#34C759") : Color.FromArgb("#FF3B30"),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,

        });
        InfoLabel.FormattedText = fs;

    }

    /*  Elimina i file temporanei della sessione precedente  */
    private void CleanupTempFiles() {
        CleanupSingleFile(ref _processed_temp_path);
        CleanupSingleFile(ref _preview_temp_path);

    }

    /*  Elimina un singolo file temporaneo  */
    private void CleanupSingleFile(ref string? file_path) {
        if (!string.IsNullOrEmpty(file_path) && File.Exists(file_path)) {
            try {
                File.Delete(file_path);

            }
            catch {
                /*  Ignora errori di pulizia file temporanei  */

            }

        }

        file_path = null;

    }

    #endregion

}
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SvgTracerMac;

/*  Client specializzato per le chiamate API a Google Gemini (Nano Banana)  */
public class GeminiClient {

    #region CAMPI PRIVATI

    private static readonly HttpClient _http_client = new() {
        Timeout = TimeSpan.FromMinutes(3)

    };

    private readonly string _api_key;
    private readonly string _model_name;

    private static readonly JsonSerializerOptions _json_options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

    };

    private const string BASE_URL =
        "https://generativelanguage.googleapis.com/v1beta/models";

    /*  Prompt indicizzati per chiave: normale, aggressivo, tecnico  */
    public static readonly Dictionary<string, string> Prompts = new() {
        ["normale"] =
            "Remove everything around the main subject of this image. " +
            "Delete all background elements, shadows, reflections, gradients, textures, watermarks, logos, and any secondary objects. " +
            "Keep ONLY the primary subject with clean, sharp edges. " +
            "Place the isolated subject on a perfectly pure white background (RGB 255,255,255). " +
            "Do not add any border, padding, or artistic effects. " +
            "The output must be a clean cutout suitable for vector tracing.",

        ["aggressivo"] =
            "Isolate ONLY the single most prominent subject in this image with surgical precision. " +
            "Remove absolutely everything else without exception: background, secondary objects, shadows, reflections, " +
            "gradients, textures, watermarks, logos, text, decorative elements, surfaces, and any peripheral detail. " +
            "If the subject is holding, wearing, or touching secondary items, remove those items too — keep only the subject's core body/form. " +
            "Apply razor-sharp edge detection with zero anti-aliasing bleed. " +
            "Place the result on a perfectly pure white background (RGB 255,255,255). " +
            "No border, no padding, no artistic interpretation. " +
            "The output must be a hard, clinical cutout suitable for vector tracing.",

        ["tecnico"] =
            "Process this image as a technical pixel-extraction task. Do NOT use scene understanding, semantic reasoning, or contextual inference. " +
            "Focus exclusively on the contiguous non-white region closest to the geometric center of the image. " +
            "Ignore any object or detail located at the periphery or edges, regardless of visual prominence or size. " +
            "Extract only the central region using hard edge detection with zero feathering. " +
            "Do not interpret what the object is — treat it as an anonymous shape. " +
            "Place the extracted central region on a perfectly pure white background (RGB 255,255,255). " +
            "No border, no padding, no contextual adjustments. " +
            "The output must be a raw technical cutout suitable for vector tracing.",

    };

    private const int MAX_RETRIES = 5;
    private static readonly TimeSpan[] _retry_delays = [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16),
        TimeSpan.FromSeconds(30),

    ];

    #endregion

    #region METODI PUBBLICI

    public GeminiClient(string api_key, string model_name = "gemini-2.5-flash-image") {
        _api_key = api_key;
        _model_name = model_name;

    }

    /*  Invia l'immagine a Gemini per la rimozione dello sfondo e restituisce i byte dell'immagine elaborata  */
    public async Task<byte[]> RemoveBackgroundAsync(string image_path, string prompt_key = "normale") {
        byte[] image_bytes = await File.ReadAllBytesAsync(image_path);
        string base64_data = Convert.ToBase64String(image_bytes);
        string mime_type = ResolveMimeType(image_path);
        string prompt_text = Prompts.GetValueOrDefault(prompt_key, Prompts["normale"])!;

        var request_body = new GeminiRequest {
            Contents = [
                new GeminiContent {
                    Parts = [
                        new GeminiPart {
                            Text = prompt_text

                        },
                        new GeminiPart {
                            InlineData = new GeminiBlob {
                                MimeType = mime_type,
                                Data = base64_data

                            }

                        }

                    ]

                }

            ],
            GenerationConfig = new GeminiGenerationConfig {
                ResponseModalities = ["IMAGE", "TEXT"],
                Temperature = 0.0f,
                TopP = 0.1f

            }

        };

        string json_payload = JsonSerializer.Serialize(request_body, _json_options);
        string url = $"{BASE_URL}/{_model_name}:generateContent?key={_api_key}";

        Exception? last_exception = null;

        for (int attempt = 0; attempt < MAX_RETRIES; attempt++) {
            try {
                byte[] result = await SendRequestAsync(url, json_payload);
                return result;

            }
            catch (HttpRequestException ex) {
                last_exception = ex;

            }
            catch (InvalidOperationException ex) {
                last_exception = ex;

            }

            /*  Attesa esponenziale prima del prossimo tentativo  */
            if (attempt < MAX_RETRIES - 1) {
                await Task.Delay(_retry_delays[attempt]);

            }

        }

        throw new Exception(
            $"All {MAX_RETRIES} attempts failed. Last error: {last_exception?.Message}",
            last_exception

        );

    }

    #endregion

    #region METODI PRIVATI

    /*  Esegue una singola chiamata all'API Gemini e restituisce i byte dell'immagine  */
    private async Task<byte[]> SendRequestAsync(string url, string json_payload) {
        using var http_content = new StringContent(
            json_payload, Encoding.UTF8, "application/json"

        );

        using var response = await _http_client.PostAsync(url, http_content);

        if (!response.IsSuccessStatusCode) {
            string error_body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Gemini API error ({(int)response.StatusCode}): {error_body}"

            );

        }

        string response_json = await response.Content.ReadAsStringAsync();

        var gemini_response = JsonSerializer.Deserialize<GeminiResponse>(
            response_json, _json_options

        );

        /*  Cerca la parte contenente l'immagine nella risposta  */
        var image_part = gemini_response?.Candidates?
            .FirstOrDefault()?.Content?.Parts?
            .FirstOrDefault(p => p.InlineData != null);

        if (image_part?.InlineData?.Data == null) {
            throw new InvalidOperationException(
                "Gemini response does not contain an image."

            );

        }

        return Convert.FromBase64String(image_part.InlineData.Data);

    }

    /*  Determina il MIME type in base all'estensione del file  */
    private static string ResolveMimeType(string file_path) {
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

    #region DTO GEMINI API

    private class GeminiRequest {
        public List<GeminiContent>? Contents { get; set; }
        public GeminiGenerationConfig? GenerationConfig { get; set; }

    }

    private class GeminiContent {
        public List<GeminiPart>? Parts { get; set; }

    }

    private class GeminiPart {
        public string? Text { get; set; }
        public GeminiBlob? InlineData { get; set; }

    }

    private class GeminiBlob {
        public string? MimeType { get; set; }
        public string? Data { get; set; }

    }

    private class GeminiGenerationConfig {
        public List<string>? ResponseModalities { get; set; }
        public float? Temperature { get; set; }
        public float? TopP { get; set; }

    }

    private class GeminiResponse {
        public List<GeminiCandidate>? Candidates { get; set; }

    }

    private class GeminiCandidate {
        public GeminiContent? Content { get; set; }
        public string? FinishReason { get; set; }

    }

    #endregion

}

using System.Reflection;
using System.Text.Json;

namespace SvgTracerMac;

/*  Carica i segreti dall'embedded resource appsettings.secrets.json  */
public static class SecretsLoader {

    #region CAMPI PRIVATI

    private const string RESOURCE_NAME = "SvgTracerMac.appsettings.secrets.json";

    #endregion

    #region METODI PUBBLICI

    /*  Restituisce la chiave API di Gemini letta dal file di segreti  */
    public static string GetGeminiApiKey() {
        try {
            var json_text = ReadEmbeddedResource(RESOURCE_NAME);

            var document = JsonDocument.Parse(json_text);
            var api_key = document.RootElement
                .GetProperty("GeminiApiKey")
                .GetString();

            if (string.IsNullOrWhiteSpace(api_key)) {
                throw new InvalidOperationException(
                    "GeminiApiKey is empty in appsettings.secrets.json."

                );

            }

            return api_key;

        }
        catch (InvalidOperationException) {
            throw;

        }
        catch (Exception ex) {
            throw new InvalidOperationException(
                $"Failed to load the Gemini API key: {ex.Message}", ex

            );

        }

    }

    #endregion

    #region METODI PRIVATI

    /*  Legge il contenuto di un embedded resource come stringa  */
    private static string ReadEmbeddedResource(string resource_name) {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resource_name);

        if (stream == null) {
            throw new InvalidOperationException(
                $"Embedded resource '{resource_name}' not found. Verify that appsettings.secrets.json exists and is set as EmbeddedResource."

            );

        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();

    }

    #endregion

}

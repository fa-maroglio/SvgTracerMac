using System.Text.Json;

namespace SvgTracerMac;

/*  Carica i segreti dal file appsettings.secrets.json installato localmente  */
public static class SecretsLoader {

    #region CAMPI PRIVATI

    private const string FILE_NAME = "appsettings.secrets.json";

    #endregion

    #region METODI PUBBLICI

    /*  Restituisce la chiave API di Gemini letta dal file di segreti  */
    public static string GetGeminiApiKey() {
        try {
            var json_text = ReadSecretsFile();

            var document = JsonDocument.Parse(json_text);
            var api_key = document.RootElement
                .GetProperty("GeminiApiKey")
                .GetString();

            if (string.IsNullOrWhiteSpace(api_key)) {
                throw new InvalidOperationException(
                    "GeminiApiKey è vuota in appsettings.secrets.json."

                );

            }

            return api_key;

        }
        catch (InvalidOperationException) {
            throw;

        }
        catch (Exception ex) {
            throw new InvalidOperationException(
                $"Errore nel caricamento della chiave API Gemini: {ex.Message}", ex

            );

        }

    }

    /*  Restituisce il percorso in cui viene cercato il file dei segreti  */
    public static string GetSecretsPath() {
        return Path.Combine(AppContext.BaseDirectory, FILE_NAME);

    }

    #endregion

    #region METODI PRIVATI

    /*  Cerca e legge appsettings.secrets.json nella cartella dell'eseguibile  */
    private static string ReadSecretsFile() {
        var path = GetSecretsPath();

        if (File.Exists(path)) {
            return File.ReadAllText(path);

        }

        throw new FileNotFoundException(
            $"File '{FILE_NAME}' non trovato.\n" +
            $"Installarlo nella cartella dell'eseguibile:\n  {path}"

        );

    }

    #endregion

}

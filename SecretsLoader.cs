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

    /*  Restituisce i percorsi in cui viene cercato il file dei segreti  */
    public static IReadOnlyList<string> GetSearchPaths() {
        return [
            Path.Combine(FileSystem.AppDataDirectory, FILE_NAME),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "SvgTracerMac", FILE_NAME
            ),
            Path.Combine(AppContext.BaseDirectory, FILE_NAME),
        ];

    }

    #endregion

    #region METODI PRIVATI

    /*  Cerca e legge appsettings.secrets.json nei percorsi standard  */
    private static string ReadSecretsFile() {
        foreach (var path in GetSearchPaths()) {
            if (File.Exists(path)) {
                return File.ReadAllText(path);

            }

        }

        var paths_list = string.Join("\n  - ", GetSearchPaths());
        throw new FileNotFoundException(
            $"File '{FILE_NAME}' non trovato.\n" +
            $"Installarlo in uno dei seguenti percorsi:\n  - {paths_list}"

        );

    }

    #endregion

}

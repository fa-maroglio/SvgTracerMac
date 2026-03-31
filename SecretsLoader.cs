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
                    "GeminiApiKey e' vuota in appsettings.secrets.json."

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
        return ResolveSecretsPath() ?? Path.Combine(AppContext.BaseDirectory, FILE_NAME);

    }

    #endregion

    #region METODI PRIVATI

    /*  Cerca e legge appsettings.secrets.json nella cartella dell'eseguibile  */
    private static string ReadSecretsFile() {
        var path = ResolveSecretsPath();

        if (!string.IsNullOrWhiteSpace(path)) {
            return File.ReadAllText(path);

        }

        throw new FileNotFoundException(
            $"File '{FILE_NAME}' non trovato.\n" +
            "Percorsi controllati:\n  " +
            string.Join("\n  ", GetCandidatePaths())

        );

    }

    /*  Cerca il file nelle cartelle di esecuzione e nelle directory padre  */
    private static string? ResolveSecretsPath() {
        foreach (var candidate_path in GetCandidatePaths()) {
            if (File.Exists(candidate_path)) {
                return candidate_path;

            }

        }

        return null;

    }

    /*  Costruisce l'elenco dei possibili percorsi da controllare  */
    private static IEnumerable<string> GetCandidatePaths() {
        var seen_paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var start_directory in GetStartDirectories()) {
            var current_directory = start_directory;

            while (!string.IsNullOrWhiteSpace(current_directory)) {
                var candidate_path = Path.Combine(current_directory, FILE_NAME);

                if (seen_paths.Add(candidate_path)) {
                    yield return candidate_path;

                }

                current_directory = Directory.GetParent(current_directory)?.FullName;

            }

        }

    }

    /*  Definisce le directory iniziali da cui partire per la ricerca  */
    private static IEnumerable<string> GetStartDirectories() {
        var directories = new[] {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
            FileSystem.AppDataDirectory
        };

        foreach (var directory in directories) {
            if (!string.IsNullOrWhiteSpace(directory)) {
                yield return directory;

            }

        }

    }

    #endregion

}

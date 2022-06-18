using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleMailArchiver.Data
{
    [Serializable]
    public record class AppConfig
    {
        public string ArchiveBasePath { get; init; } = "";

        public string ImportBasePath { get; init; } = "";

        public string AccountConfigsPath { get; init; } = "";

        public string DbPath { get; init; } = "";

        [JsonIgnore]
        public List<Account> Accounts { get; } = new();

        public static AppConfig Load()
        {
            AppConfig config;

            string archiveBasePathEnvVariable = "SMA_ARCHIVEBASEPATH";
            string importBasePathEnvVariable = "SMA_IMPORTBASEPATH";
            string accountConfigsPathEnvVariable = "SMA_ACCOUNTSCONFIGPATH";
            string dbPathEnvVariable = "SMA_DBPATH";

#nullable enable
            string? archiveBasePathEnv = Environment.GetEnvironmentVariable(archiveBasePathEnvVariable);
            string? importBasePathEnv = Environment.GetEnvironmentVariable(importBasePathEnvVariable);
            string? accountConfigsPathEnv = Environment.GetEnvironmentVariable(accountConfigsPathEnvVariable);
            string? dbPathEnv = Environment.GetEnvironmentVariable(dbPathEnvVariable);
#nullable disable
            if (archiveBasePathEnv != null && importBasePathEnv != null && accountConfigsPathEnv != null && dbPathEnv != null)
            {
                config = new()
                {
                    ArchiveBasePath = archiveBasePathEnv,
                    ImportBasePath = importBasePathEnv,
                    AccountConfigsPath = accountConfigsPathEnv,
                    DbPath = dbPathEnv
                };
            }
            else
            {
                string configPath = "config.json";

                try
                {
                    var tmp = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath));
                    if (tmp != null)
                        config = tmp;
                    else
                        throw new JsonException("Element null after deserialization");
                }
                catch (JsonException ex)
                {
                    throw new InvalidDataException($"Could not load file {configPath} as AppConfig", ex);
                }
            }
            if (config is null)
                throw new InvalidDataException($"Could not load config.");

            return config;
        }

        public static AppConfig LoadAccounts(AppConfig config)
        {
            if (Program.Logger == null)
                throw new InvalidOperationException("Execute LoadAccounts only after setting up the logger");
            if (Directory.Exists(config.AccountConfigsPath))
            {
                var files = Directory.GetFiles(config.AccountConfigsPath, "*.account", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        var acc = JsonSerializer.Deserialize<Account>(File.ReadAllText(file));
                        if (acc != null)
                        {
                            acc.AccountFilename = Path.GetFileName(file);
                            config.Accounts.Add(acc);
                            Program.Logger.LogInformation($"Found account {acc.AccountFilename}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Program.Logger.LogError($"Loading account {file} failed with error {ex.Message}");
                    }
                }
            }

            return config;
        }
    }
}


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

            string? archiveBasePathEnv = Environment.GetEnvironmentVariable(archiveBasePathEnvVariable);
            string? importBasePathEnv = Environment.GetEnvironmentVariable(importBasePathEnvVariable);
            string? accountConfigsPathEnv = Environment.GetEnvironmentVariable(accountConfigsPathEnvVariable);
            string? dbPathEnv = Environment.GetEnvironmentVariable(dbPathEnvVariable);

            if (archiveBasePathEnv != null && importBasePathEnv != null && accountConfigsPathEnv != null && dbPathEnv != null)
            {
                Program.Logger.LogInformation("Using Environment variables for configuration");
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
                Program.Logger.LogInformation($"Using file {configPath} for configuration");

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

            Program.Logger.LogInformation(
                "Using conifg:\n\t" +
                $"Account configs path: {config.AccountConfigsPath}\n\t" +
                $"Import base path: {config.ImportBasePath}\n\t"+
                $"Archive base path: {config.ArchiveBasePath}\n\t" +
                $"Database path: {config.DbPath}"
                );

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
                    catch (JsonException)
                    {

                    }
                }
            }

            return config;
        }
    }
}


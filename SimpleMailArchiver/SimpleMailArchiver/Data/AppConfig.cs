using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleMailArchiver.Data
{
    [Serializable]
    public record class AppConfig
    {
        public string ArchiveBasePath {
            get => archiveBasePath;
            set
            {
                if (configReadonly)
                    throw new Exception("Configuration cannot be modified after setup.");
                archiveBasePath = value;
            }
        }

        public string ImportBasePath {
            get => importBasePath;
            set
            {
                if (configReadonly)
                    throw new Exception("Configuration cannot be modified after setup.");
                importBasePath = value;
            }
        }

        public string AccountConfigsPath
        {
            get => accountConfigsPath;
            set
            {
                if (configReadonly)
                    throw new Exception("Configuration cannot be modified after setup.");
                accountConfigsPath = value;
            }
        }

        public string DbPath
        {
            get => dbPath;
            set
            {
                if (configReadonly)
                    throw new Exception("Configuration cannot be modified after setup.");
                dbPath = value;
            }
        }

        [JsonIgnore]
        public List<Account> Accounts { get; } = new();
        private bool configReadonly;
        private string archiveBasePath;
        private string importBasePath;
        private string accountConfigsPath;
        private string dbPath;

        public static AppConfig Load(string configPath)
        {
            AppConfig config;
            try
            {
                config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath));
            }
            catch (JsonException)
            {
                throw new InvalidDataException($"Could not load file {configPath} as AppConfig");
            }
            if (config is null)
                throw new InvalidDataException($"Could not load file {configPath} as AppConfig");
            config.configReadonly = true;

            if (Directory.Exists(config.AccountConfigsPath))
            {
                var files = Directory.GetFiles(config.AccountConfigsPath, "*.account", SearchOption.TopDirectoryOnly);
                int accCount = 0;
                foreach (var file in files)
                {
                    try
                    {
                        var acc = JsonSerializer.Deserialize<Account>(File.ReadAllText(file));
                        if (acc != null)
                            acc.ID = accCount++;
                        config.Accounts.Add(acc);
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


using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SimpleMailArchiver.Data;

public class ApplicationContext
{
    public ApplicationContext(PathConfig config, ILoggerFactory loggerFactory)
    {
        PathConfig = config;
        LoadAccounts();
        ImportManager = new ImportManager(loggerFactory);
    }

    public PathConfig PathConfig { get; }
    public ImportManager ImportManager { get; }
    public List<Account> Accounts { get; private set; }

    [MemberNotNull(nameof(Accounts))]
    private void LoadAccounts()
    {
        Accounts = [];

        var files = Directory.GetFiles(PathConfig.AccountConfigsPath, "*.account", SearchOption.TopDirectoryOnly);
        Accounts.Capacity = files.Length;
        foreach (var file in files)
            try
            {
                var acc = JsonSerializer.Deserialize<Account>(File.ReadAllText(file));
                if (acc != null)
                {
                    acc.AccountFilename = Path.GetFileName(file);
                    Accounts.Add(acc);
                }
                else
                {
                    throw new InvalidDataException($"Parsing {file} failed");
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Parsing {file} failed with error {ex.Message}", ex);
            }
    }
}
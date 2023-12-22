using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SimpleMailArchiver.Data;

public class ApplicationContext
{
    public PathConfig PathConfig { get; }
    public ImportProgress? ImportProgress { get; set; }
    public bool ImportRunning { get; set; }
    public List<Account> Accounts { get; private set; }

    public ApplicationContext(PathConfig config)
    {
        PathConfig = config;
        LoadAccounts();
    }
    

    [MemberNotNull(nameof(Accounts))]
    private void LoadAccounts()
    {
        Accounts = new List<Account>();

        var files = Directory.GetFiles(PathConfig.AccountConfigsPath, "*.account", SearchOption.TopDirectoryOnly);
        Accounts.Capacity = files.Length;
        foreach (var file in files)
        {
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
}
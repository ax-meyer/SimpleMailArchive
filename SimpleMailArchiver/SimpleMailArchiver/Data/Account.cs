using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SimpleMailArchiver.Data;

[Serializable]
public class Account
{
    public List<FolderOptions> FolderOptions { get; set; } = new List<FolderOptions>();

    public string AccountDisplayName { get; init; } = "AccountNameToDisplay";
    
    public string Username { get; init; } = "Username";
    
    public string Password { get; init; } = "SecretPassword";
    
    public string ImapUrl { get; init; } = "ImapServerURL";

    public int DeleteAfterDays { get; init; } = -1;


    public Account()
    {
    }

    private bool accountReadonly = false;
    private string accountFilename = "";

    [JsonIgnore]
    public string AccountFilename
    {
        get => accountFilename;
        set
        {
            if (accountReadonly)
                throw new Exception("Account cannot be modified after loading.");
            accountFilename = value;
            accountReadonly = true;
        }
    }
}


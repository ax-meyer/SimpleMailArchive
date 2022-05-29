using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SimpleMailArchiver.Data;

[Serializable]
public record struct Account
{
    private string accountDisplayName;
    private string username;
    private string password;
    private string imapUrl;
    private int iD;
    private int deleteAfterDays;
    private bool accountReadonly;
    private List<string> foldersToIgnore;

    public List<string> FoldersToIgnore {
        get
        {
            if (accountReadonly)
                throw new Exception("Use IgnoreFolders to get read-only access.");
            return foldersToIgnore;
        }
        set
        {
            if (accountReadonly)
                throw new Exception("Account cannot be modified after loading.");
            foldersToIgnore = value;
        }
    }
    
    [JsonIgnore]
    public ReadOnlyCollection<string> IgnoreFolders => foldersToIgnore.AsReadOnly();

    public string AccountDisplayName
    {
        get => accountDisplayName;
        set
        {
            if (accountReadonly)
                throw new Exception("Account cannot be modified after loading.");
            accountDisplayName = value;
        }
    }

    public string Username
    {
        get => username;
        set
        {
            if (accountReadonly)
                throw new Exception("Account cannot be modified after loading.");
            username = value;
        }
    }

    public string Password
    {
        get => password;
        set
        {
            if (accountReadonly)
                throw new Exception("Account cannot be modified after loading.");
            password = value;
        }
    }

    public string ImapUrl
    {
        get => imapUrl; set
        {
            if (accountReadonly)
                throw new Exception("Account cannot be modified after loading.");
            imapUrl = value;
        }
    }

    public int DeleteAfterDays
    {
        get => deleteAfterDays;
        set
        {
            if (accountReadonly)
                throw new Exception("Account cannot be modified after loading.");
            deleteAfterDays = value;
        }
    }

    [JsonIgnore]
    public int ID
    {
        get => iD;
        set
        {
            if (accountReadonly)
                throw new Exception("Account cannot be modified after loading.");
            iD = value;
            accountReadonly = true;
        }
    }
}


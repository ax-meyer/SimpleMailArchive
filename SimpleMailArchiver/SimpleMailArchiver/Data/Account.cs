using System.Text.Json.Serialization;

namespace SimpleMailArchiver.Data;

[Serializable]
public class Account
{
    /// <summary>
    /// List of folders that should be treated special in some way.
    /// Any folder not in here will be archived with default settings.
    /// </summary>
    public List<FolderOptions> FolderOptions { get; set; } = new List<FolderOptions>();

    /// <summary>
    /// Name of the account to display in the GUI.
    /// </summary>
    public string AccountDisplayName { get; init; } = "AccountNameToDisplay";

    /// <summary>
    /// Username for login
    /// </summary>
    public string Username { get; init; } = "Username";

    /// <summary>
    /// Password for login
    /// </summary>
    public string Password { get; init; } = "SecretPassword";

    /// <summary>
    /// URL to IMAP server
    /// </summary>
    public string ImapUrl { get; init; } = "ImapServerURL";

    /// <summary>
    /// Messages will be deleted on the server after this amount of days.
    /// Negative numbers mean no deletion.
    /// Can be overriden with <see cref="FolderOptions"/> for individual folders.
    /// </summary>
    public int DeleteAfterDays { get; init; } = -1;

    /// <summary>
    /// Base bath in the archive for this account.
    /// </summary>
    public string BasePathInArchive { get; init; }


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


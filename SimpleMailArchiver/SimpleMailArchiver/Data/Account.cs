using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SimpleMailArchiver.Data;

[Serializable]
public class Account
{
    private string _accountFilename = "";

    private bool _accountReadonly;

    /// <summary>
    ///     List of folders that should be treated special in some way.
    ///     Any folder not in here will be archived with default settings.
    /// </summary>
    public List<FolderOptions>? FolderOptions { get; set; }

    /// <summary>
    ///     Name of the account to display in the GUI.
    /// </summary>
    [Required]
    public required string AccountDisplayName { get; init; }

    /// <summary>
    ///     Username for login
    /// </summary>
    [Required]
    public required string Username { get; init; }

    /// <summary>
    ///     Password for login
    /// </summary>
    [Required]
    public required string Password { get; init; }

    /// <summary>
    ///     URL to IMAP server
    /// </summary>
    [Required]
    public required string ImapUrl { get; init; }

    /// <summary>
    ///     Messages will be deleted on the server after this amount of days.
    ///     Negative numbers mean no deletion.
    ///     Can be overriden with <see cref="FolderOptions" /> for individual folders.
    /// </summary>
    public int DeleteAfterDays { get; init; } = -1;

    /// <summary>
    ///     Base bath in the archive for this account. With this option, emails from multiple accounts can be separated.
    ///     CAREFUL: This is just a separation in the folder structure - all emails will still be accessible through the
    ///     website, at the moment there is no way to use a single instance for multiple users who shall not see all emails in
    ///     the archive.
    /// </summary>
    public string? BasePathInArchive { get; init; }

    [JsonIgnore]
    public string AccountFilename
    {
        get => _accountFilename;
        set
        {
            if (_accountReadonly)
                throw new Exception("Account cannot be modified after loading.");
            _accountFilename = value;
            _accountReadonly = true;
        }
    }
}
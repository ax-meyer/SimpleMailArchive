namespace SimpleMailArchiver.Data;

[Serializable]
public class FolderOptions
{
    /// <summary>
    ///     Name of the folder on the server. Required.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Folder path in the archive. If empty, <see cref="Name" /> will be used.
    ///     Useful to e.g. consolidate multiple accounts into a single folder structure.
    /// </summary>
    public string? NameInArchive { get; init; }

    /// <summary>
    ///     If true, folder will be excluded from archiving.
    /// </summary>
    public bool Exclude { get; init; } = false;

    /// <summary>
    ///     If not null, overrides the account setting for DeleteAfterDays. Negative values mean no deletion.
    ///     E-Mails will be deleted on the server after this period of time.
    /// </summary>
    public int? DeleteAfterDays { get; init; }

    /// <summary>
    ///     Archived folder will mirror the server folder - deletions on server are mirrored in the archive!
    /// </summary>
    public bool SyncServerFolder { get; init; } = false;
}
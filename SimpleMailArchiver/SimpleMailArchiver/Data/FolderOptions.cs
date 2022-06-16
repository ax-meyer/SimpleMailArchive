using System;
namespace SimpleMailArchiver.Data;

[Serializable]
public class FolderOptions
{
    public FolderOptions()
    {
    }

    /// <summary>
    /// Name of the folder. Required.
    /// </summary>
    public string Name { get; init; } = "FolderName";

    /// <summary>
    /// Folder path in the archive. If empty, <see cref="Name"/> will be used.
    /// Usefull to e.g. consolidate multiple accounts into a single folder structure.
    /// </summary>
    public string NameInAchrive { get; init; }

	/// <summary>
    /// If true, folder will be excluded from archiving.
    /// </summary>
	public bool Exclude { get; init; } = false;

	/// <summary>
    /// If not null, overrides the account setting for DeleteAfterDays. Negative values mean no deletion.
    /// E-Mails will be deleted on the server after this period of time.
    /// </summary>
	public int? DeleteAfterDays { get; init; } = null;

	/// <summary>
    /// Archived folder will mirror the server folder - deletions on server are done in archive!
    /// </summary>
	public bool SyncServerFolder { get; init; } = false;
}



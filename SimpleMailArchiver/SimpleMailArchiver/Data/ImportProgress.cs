namespace SimpleMailArchiver.Data;

public class ImportProgress
{
	public int TotalMessageCount { get; set; }
	public int ParsedMessageCount { get; set; }
	public int ImportedMessageCount { get; set; }
	public int LocalMessagesDeletedCount { get; set; }
	public int RemoteMessagesDeletedCount { get; set; }
	public string CurrentFolder { get; set; }
	public string InfoMessage { get; set; }
	public CancellationToken Ct { get; set; }
}

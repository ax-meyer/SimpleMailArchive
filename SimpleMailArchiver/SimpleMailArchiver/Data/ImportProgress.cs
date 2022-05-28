namespace SimpleMailArchiver.Data;

public class ImportProgress
{
	public int TotalMessageCount { get; set; }
	public int ParsedMessageCount { get; set; }
	public int ImportedMessageCount { get; set; }
	public string CurrentFolder { get; set; }
	public CancellationToken Ct { get; set; }
}

using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services;

public class FileDownloadHelperContext
{
    public byte[] FileContent { get; set; } = { 0 };
    public string FileName { get; set; } = "empty";

    private readonly MailMessageHelperService _messageHelperService;

    public FileDownloadHelperContext(MailMessageHelperService messageHelperService)
    {
        _messageHelperService = messageHelperService;
    }


    public async Task PrepareMessageForDownload(MailMessage message, CancellationToken token = default)
    {
        FileContent = await File.ReadAllBytesAsync(_messageHelperService.GetEmlPath(message), token);
        FileName = "mail.eml";
    }

    public Task ResetDownload()
    {
        FileContent = new byte[] { 0 };
        FileName = "empty";
        return Task.CompletedTask;
    }
}
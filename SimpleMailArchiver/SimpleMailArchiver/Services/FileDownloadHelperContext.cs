using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services;

public class FileDownloadHelperContext(MailMessageHelperService messageHelperService)
{
    public byte[] FileContent { get; private set; } = [0];
    public string FileName { get; private set; } = "empty";

    public async Task PrepareMessageForDownload(MailMessage message, CancellationToken token)
    {
        FileContent = await File.ReadAllBytesAsync(messageHelperService.GetEmlPath(message), token);
        FileName = "mail.eml";
    }

    public Task ResetDownload()
    {
        FileContent = [0];
        FileName = "empty";
        return Task.CompletedTask;
    }
}
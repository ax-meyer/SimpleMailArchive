using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SimpleMailArchiver.Services;

namespace SimpleMailArchiver.Pages;

public class FileDownloadsModel(MailMessageHelperService helperService, ILogger<FileDownloadsModel> logger) : PageModel
{
    public async Task<IActionResult> OnGet(int id)
    {
        logger.LogInformation("Downloading EML file for message ID {MessageId}", id);
        var path = helperService.GetEmlPath(id);
        if (!System.IO.File.Exists(path))
        {
            logger.LogWarning("File {Path} does not exist", path);
            return NotFound();
        }

        var content = await System.IO.File.ReadAllBytesAsync(path);
        return File(content, "message/rfc822", "mail.eml");
    }
}
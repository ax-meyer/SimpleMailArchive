// csharp

using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SimpleMailArchiver.Data;
using SimpleMailArchiver.Services;

namespace SimpleMailArchiver.Pages;

public class FileDownloadsModel(MailMessageHelperService helperService) : PageModel
{
    public async Task<IActionResult> OnGet(int id)
    {
        var path = helperService.GetEmlPath(id);
        if (!System.IO.File.Exists(path)) return NotFound();

        var content = await System.IO.File.ReadAllBytesAsync(path);
        return File(content, "message/rfc822", "mail.eml");
    }
}
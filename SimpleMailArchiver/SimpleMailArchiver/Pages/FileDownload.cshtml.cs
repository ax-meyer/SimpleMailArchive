using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SimpleMailArchiver.Services;

namespace SimpleMailArchiver.Pages;

public class FileDownloadsModel(FileDownloadHelperContext helperContext) : PageModel
{
    public Task<IActionResult> OnGet()
    {
        return Task.FromResult<IActionResult>(File(helperContext.FileContent, "application/force-download", helperContext.FileName));
    }
}
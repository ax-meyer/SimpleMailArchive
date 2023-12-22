using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SimpleMailArchiver.Services;

namespace SimpleMailArchiver.Pages
{
    public class FileDownloadsModel : PageModel
    {
        private readonly FileDownloadHelperContext _helperContext;
        
        public FileDownloadsModel(FileDownloadHelperContext helperContext)
        {
            _helperContext = helperContext;
        }
        public Task<IActionResult> OnGet()
        {
            return Task.FromResult<IActionResult>(File(_helperContext.FileContent, "application/force-download", _helperContext.FileName));
        }
    }
}


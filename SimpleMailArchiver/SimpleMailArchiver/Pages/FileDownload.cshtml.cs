using SimpleMailArchiver.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SimpleMailArchiver.Pages
{
    public class FileDownloadsModel : PageModel
    {
        private static byte[] fileContent = new byte[] { 0 };
        private static string fileName = "empty";

        public static async Task PrepareMessageForDownload(MailMessage message, CancellationToken token = default)
        {
            fileContent = await System.IO.File.ReadAllBytesAsync(message.EmlPath, token);
            fileName = "mail.eml";
        }

        public static async Task ResetDownload()
        {
            fileContent = new byte[] { 0 };
            fileName = "empty";
        }

        
        public async Task<IActionResult> OnGet()
        {
            if (fileContent == null)
                fileContent = new byte[] { 0 };
            if (fileName.Trim() == string.Empty)
                fileName = "empty";

            return File(fileContent!, "application/force-download", fileName);
        }
    }
}


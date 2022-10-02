using MimeKit;
using System.Text;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace SimpleMailArchiver.Data
{
	public class Utils
	{
		public static async Task<string> CreateMailHash(MailMessage message, CancellationToken token = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var strData = message.Date.ToString("dd.MM.yyyy-HH:mm:ss");
            strData += message.Subject;
            strData += message.Sender.ToString();
            strData += message.Recipient.ToString();
            strData += message.CC_recipient.ToString();
            strData += message.BCC_recipient.ToString();

            byte[] encodedMessage = Encoding.UTF8.GetBytes(strData);
            using var alg = SHA256.Create();
            string hex = "";
            
            var hashValue = await alg.ComputeHashAsync(new MemoryStream(encodedMessage), token).ConfigureAwait(false);
            foreach (byte x in hashValue)
            {
                hex += string.Format("{0:x2}", x);
            }
            return hex;
        }

        public static async Task<bool> SaveMessage(MimeMessage mimeMessage, string folder, ArchiveContext context, CancellationToken token = default)
        {
            var mailMessage = await MailMessage.Construct(mimeMessage, folder, token).ConfigureAwait(false);

            if (await context.MailMessages.AnyAsync(o => o.Hash == mailMessage.Hash, token).ConfigureAwait(false))
                return false;

            token.ThrowIfCancellationRequested();

            await context.AddAsync(mailMessage).ConfigureAwait(false);
            await context.SaveChangesAsync().ConfigureAwait(false);
            var msgFromDb = await context.MailMessages.Where(m => m.Hash == mailMessage.Hash).FirstAsync().ConfigureAwait(false);
            
            Directory.CreateDirectory(Path.GetDirectoryName(msgFromDb.EmlPath));
            await mimeMessage.WriteToAsync(msgFromDb.EmlPath).ConfigureAwait(false);
            return true;
        }
	}
}

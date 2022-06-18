using MimeKit;
using System.Text;
using System.Security.Cryptography;


namespace SimpleMailArchiver.Data
{
	public class ParseMailMessage
	{
		public static async Task<string> CreateMailHash(MailMessage message!!, CancellationToken token = default)
        {
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

        public static string MailSavePath(MailMessage message!!)
        {            
            string path = Program.Config.ArchiveBasePath.Trim();
            if (path.Length > 0) { path = path.TrimEnd('/') + "/"; }
            path += message.Folder.TrimEnd('/');
            path += "/";
            Directory.CreateDirectory(path);
            path += message.Hash;
            path += ".eml";
            return path;
        }
	}
}

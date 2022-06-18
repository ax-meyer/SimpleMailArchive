using MimeKit;
using System.Text;
using System.Security.Cryptography;


namespace SimpleMailArchiver.Data
{
	public class ParseMailMessage
	{
		public static async Task<string> CreateMailHash(MimeMessage message!!, CancellationToken token = default)
        {
            var strData = message.Date.ToString();
            strData += message.Subject;
            strData += message.From.ToString();
            strData += message.To.ToString();
            strData += message.Cc.ToString();
            strData += message.Bcc.ToString();

            byte[] encodedMessage = Encoding.UTF8.GetBytes(strData);
            using SHA512 alg = SHA512.Create();
            string hex = "";

            var hashValue = await alg.ComputeHashAsync(new MemoryStream(encodedMessage), token);
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

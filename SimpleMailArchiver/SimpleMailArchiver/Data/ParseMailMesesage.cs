using MimeKit;
using System.Text;
using System.Security.Cryptography;


namespace SimpleMailArchiver.Data
{
	public class ParseMailMessage
	{
		public static void ParseMailMesesageToStorage(MimeKit.MimeMessage message!!, string folder)
		{
            using ArchiveContext? context = Program.ContextFactory.CreateDbContext();
            MailMessage mailMessage = new(message, folder);
            if (context.MailMessages.Any(o => o.Hash == mailMessage.Hash)) { throw new DuplicateMessageException(); }

            context.Add(mailMessage);
            message.WriteTo(MailSavePath(mailMessage));
            context.SaveChanges();
        }

		public static string CreateMailHash(MimeMessage message!!)
        {
            var strData = message.Date.ToString();
            strData += message.Subject;
            strData += message.From.ToString();
            strData += message.To.ToString();
            strData += message.Cc.ToString();
            strData += message.Bcc.ToString();

            byte[]? encodedMessage = Encoding.UTF8.GetBytes(strData);
            using SHA512? alg = SHA512.Create();
            string hex = "";

            var hashValue = alg.ComputeHash(encodedMessage);
            foreach (byte x in hashValue)
            {
                hex += String.Format("{0:x2}", x);
            }
            return hex;
        }

        public static string MailSavePath(MailMessage message!!)
        {            
            string path = Program.Config.ArchiveBasePath.Trim();
            if (path.Length > 0 && !path.EndsWith("/")) { path += "/"; }
            path += message.Folder;
            path += "/";
            Directory.CreateDirectory(path);
            path += message.Hash;
            path += ".eml";
            return path;
        }
	}
}


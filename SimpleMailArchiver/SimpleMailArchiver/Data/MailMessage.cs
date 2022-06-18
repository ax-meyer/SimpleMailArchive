using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MimeKit;
using System.Diagnostics.CodeAnalysis;

namespace SimpleMailArchiver.Data;

/// <summary>
/// Table headers to display a mail message.
/// </summary>
public enum TableHeader
{
    Date,
    Subject,
    Sender,
    Recipient,
    Folder,
    Attachments
}

public partial class MailMessage : IEquatable<MailMessage>
{

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long ID { get; set; }
    public string Hash { get; set; }
    public string Subject { get; set; }
    public string Sender { get; set; }
    public string Recipient { get; set; }
    public string CC_recipient { get; set; }
    public string BCC_recipient { get; set; }
    public DateTime Date { get; set; }
    public string Attachments { get; set; }
    public string Folder { get; set; }
    public string TextBody { get; set; }
    public string HtmlBody { get; set; }

    [NotMapped]
    public string EmlPath => (Program.Config.ArchiveBasePath + "/" + Folder + "/message-" + ID.ToString() + ".eml").Replace("//", "/");

    public MailMessage() { }

    public static async Task<MailMessage> Construct(MimeMessage mimeMessage!!, string folder, CancellationToken token = default)
    {
        // generate list of attachment filenames
        List<string> attachment_names = new();
        attachment_names.AddRange(mimeMessage.Attachments.Select(attachment => attachment.ContentDisposition?.FileName ?? attachment.ContentType.Name));
        
        foreach (var part in mimeMessage.BodyParts)
        {
            if (part.ContentDisposition != null && part.ContentDisposition.FileName != null)
            {
                var name = part.ContentDisposition.FileName;
                if (!attachment_names.Contains(name))
                    attachment_names.Add(name);
            }
            else if (part.ContentType != null && part.ContentType.Name != null)
            {
                var name = part.ContentType.Name;
                if (!attachment_names.Contains(name))
                    attachment_names.Add(part.ContentType.Name);
            }
        }

        var msg = new MailMessage()
        {
            Subject = mimeMessage.Subject,
            Sender = mimeMessage.From.ToString(),
            Recipient = mimeMessage.To.ToString(),
            CC_recipient = mimeMessage.Cc.ToString(),
            BCC_recipient = mimeMessage.Bcc.ToString(),
            Date = mimeMessage.Date.DateTime,
            Attachments = JsonSerializer.Serialize(attachment_names),
            Folder = folder,
            TextBody = mimeMessage.TextBody,
            HtmlBody = mimeMessage.HtmlBody
        };

        msg.Hash = await ParseMailMessage.CreateMailHash(msg, token).ConfigureAwait(false);
        return msg;
    }

    public override bool Equals(object obj)
    {
        if (obj is MailMessage other)
        {
            bool equal = true;
            foreach (var prop in this.GetType().GetProperties())
            {
                if (prop.Name == nameof(MailMessage.ID) || prop.Name == nameof(MailMessage.EmlPath))
                    continue;
                else if (prop.Name == nameof(MailMessage.Date))
                {
                    DateTime first = this.Date;
                    DateTime second = other.Date;
                    string fmt = "dd.MM.yyyy-HH:mm:ss";
                    equal = first.ToString(fmt) == second.ToString(fmt);
                }
                else
                    equal = prop.GetValue(this) == prop.GetValue(other);
            }
            return equal;
        }
        else
            throw new InvalidDataException();
    }

    public bool Equals(MailMessage other)
    {
        return Equals((object)other);
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}

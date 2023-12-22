using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MimeKit;

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

public class MailMessage : IEquatable<MailMessage>
{
    public long Id { get; init; }
    [Required] public required string Hash { get; set; }
    [Required] public required string Subject { get; init; }
    [Required] public required string Sender { get; init; }
    [Required] public required string Recipient { get; init; }
    public string? CcRecipient { get; init; }
    public string? BccRecipient { get; init; }
    [Required] public required DateTime Date { get; init; }
    public string? Attachments { get; init; }
    [Required] public required string Folder { get; set; }
    [Required] public required string TextBody { get; init; }
    [Required] public required string HtmlBody { get; init; }

    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (obj is MailMessage other)
        {
            bool equal = true;
            foreach (var prop in GetType().GetProperties())
            {
                switch (prop.Name)
                {
                    case nameof(Id):
                        continue;
                    case nameof(Date):
                    {
                        DateTime first = Date;
                        DateTime second = other.Date;
                        string fmt = "dd.MM.yyyy-HH:mm:ss";
                        equal = first.ToString(fmt) == second.ToString(fmt);
                        break;
                    }
                    default:
                        equal = prop.GetValue(this) == prop.GetValue(other);
                        break;
                }
            }

            return equal;
        }

        throw new InvalidDataException();
    }

    public bool Equals(MailMessage? other)
    {
        if (other is null)
            return false;
        return Equals((object)other);
    }

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}

public static class MailParser
{
    public static async Task<MailMessage> Construct(MimeMessage mimeMessage, string folder,
        CancellationToken token = default)
    {
        if (mimeMessage == null) throw new ArgumentNullException(nameof(mimeMessage));

        // generate list of attachment filenames
        List<string> attachmentNames = new();
        attachmentNames.AddRange(mimeMessage.Attachments.Select(attachment =>
            attachment.ContentDisposition?.FileName ?? attachment.ContentType.Name));

        foreach (var part in mimeMessage.BodyParts)
        {
            if (part.ContentDisposition != null && part.ContentDisposition.FileName != null)
            {
                var name = part.ContentDisposition.FileName;
                if (!attachmentNames.Contains(name))
                    attachmentNames.Add(name);
            }
            else if (part.ContentType != null && part.ContentType.Name != null)
            {
                var name = part.ContentType.Name;
                if (!attachmentNames.Contains(name))
                    attachmentNames.Add(part.ContentType.Name);
            }
        }

        var subject = mimeMessage.Subject;
        var sender = mimeMessage.From.ToString();
        var recipient = mimeMessage.To.ToString();
        var ccRecipient = mimeMessage.Cc.ToString();
        var bccRecipient = mimeMessage.Bcc.ToString();
        var date = mimeMessage.Date.DateTime;
        var hash = await MailMessageHelperService
            .CreateMailHash(date, subject, sender, recipient, ccRecipient, bccRecipient, token);

        var msg = new MailMessage
        {
            Subject = subject,
            Sender = sender,
            Recipient = recipient,
            CcRecipient = ccRecipient,
            BccRecipient = bccRecipient,
            Date = date,
            Attachments = JsonSerializer.Serialize(attachmentNames),
            Folder = folder,
            TextBody = mimeMessage.TextBody,
            HtmlBody = mimeMessage.HtmlBody,
            Hash = hash
        };

        return msg;
    }
}
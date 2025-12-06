using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MimeKit;
using SimpleMailArchiver.Services;

namespace SimpleMailArchiver.Data;

public record MailMessage
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
    public string TextBody { get; init; } = string.Empty;
    public string HtmlBody { get; init; } = string.Empty;

    public bool HasAttachments =>
        !string.IsNullOrEmpty(Attachments) && JsonSerializer.Deserialize<string[]>(Attachments)?.Length > 0;

    public int? NumberOfAttachments => string.IsNullOrEmpty(Attachments)
        ? null
        : JsonSerializer.Deserialize<string[]>(Attachments)?.Length;
}

public static class MailParser
{
    public static async Task<MailMessage> Construct(MimeMessage mimeMessage, string folder,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(mimeMessage);

        // generate list of attachment filenames
        List<string> attachmentNames = [];
        attachmentNames.AddRange(mimeMessage.Attachments.Select(attachment =>
            attachment.ContentDisposition?.FileName ?? attachment.ContentType.Name));

        foreach (var part in mimeMessage.BodyParts)
            if (part.ContentDisposition is { FileName: not null })
            {
                var name = part.ContentDisposition.FileName;
                if (!attachmentNames.Contains(name))
                    attachmentNames.Add(name);
            }
            else if (part.ContentType is { Name: not null })
            {
                var name = part.ContentType.Name;
                if (!attachmentNames.Contains(name))
                    attachmentNames.Add(part.ContentType.Name);
            }

        var subject = mimeMessage.Subject;
        var sender = mimeMessage.From.ToString();
        var recipient = mimeMessage.To.ToString();
        var ccRecipient = mimeMessage.Cc.ToString();
        var bccRecipient = mimeMessage.Bcc.ToString();
        var date = mimeMessage.Date.DateTime;
        var hash = await MailMessageHelperService.CreateMailHash(
            date,
            subject,
            sender,
            recipient,
            ccRecipient,
            bccRecipient,
            token
        );

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
            TextBody = mimeMessage.TextBody ?? string.Empty,
            HtmlBody = mimeMessage.HtmlBody ?? string.Empty,
            Hash = hash
        };

        return msg;
    }
}
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services;

public class MailMessageHelperService(ApplicationContext appContext, IDbContextFactory<ArchiveContext> dbContextFactory)
{
    public string GetEmlPath(MailMessage message)
    {
        return (appContext.PathConfig.ArchiveBasePath + "/" + message.Folder + "/message-" + message.Id + ".eml")
            .Replace("//", "/");
    }

    public static async Task<string> CreateMailHash(MailMessage message, CancellationToken token = default)
    {
        return await CreateMailHash(message.Date, message.Subject, message.Sender, message.Recipient,
            message.CcRecipient, message.BccRecipient, token);
    }

    public static async Task<string> CreateMailHash(DateTime date, string subject, string sender, string recipient,
        string? ccRecipient, string? bccRecipient, CancellationToken token = default)
    {
        var strData = date.ToString("dd.MM.yyyy-HH:mm:ss");
        strData += subject;
        strData += sender;
        strData += recipient;
        strData += ccRecipient;
        strData += bccRecipient;

        var encodedMessage = Encoding.UTF8.GetBytes(strData);
        using var alg = SHA256.Create();
        var hex = "";

        var hashValue = await alg.ComputeHashAsync(new MemoryStream(encodedMessage), token);
        foreach (var x in hashValue) hex += $"{x:x2}";

        return hex;
    }

    public async Task<bool> SaveMessage(MimeMessage mimeMessage, string folder, CancellationToken token = default)
    {
        var mailMessage = await MailParser.Construct(mimeMessage, folder, token);

        await using var context = await dbContextFactory.CreateDbContextAsync(token);
        if (await context.MailMessages.AnyAsync(o => o.Hash == mailMessage.Hash, token)) return false;

        token.ThrowIfCancellationRequested();

        await context.AddAsync(mailMessage, CancellationToken.None);

        var emlPath = GetEmlPath(mailMessage);
        var parentFolder = Path.GetDirectoryName(emlPath);
        if (parentFolder is null)
            throw new InvalidOperationException($"Could not extract parent directory from {emlPath}");

        Directory.CreateDirectory(parentFolder);
        await mimeMessage.WriteToAsync(emlPath, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);
        return true;
    }
}
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services;

public class MailMessageHelperService(ApplicationContext appContext, IDbContextFactory<ArchiveContext> dbContextFactory, ILogger<MailMessageHelperService> logger)
{
    public string GetEmlPath(int messageId)
    {
        using var dbContext = dbContextFactory.CreateDbContext();
        var message = dbContext.MailMessages.AsNoTracking().First(o => o.Id == messageId);
        return GetEmlPath(message);
    }
    
    public string GetEmlPath(MailMessage message) =>
        (appContext.PathConfig.ArchiveBasePath + "/" + (message.Folder + "/message-" + message.Id + ".eml").Replace("//", "/")).Replace("//", "/");
    
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
        if (await context.MailMessages.FirstOrDefaultAsync(o => o.Hash == mailMessage.Hash, token) is
            { } existingMessage)
        {
            logger.LogInformation("Message with UID {Uid} already exists as ID {Id}, just saving the file again", mimeMessage.MessageId, existingMessage.Id);
            mailMessage = existingMessage;
        }
        else
        {
            await context.AddAsync(mailMessage, CancellationToken.None);
            await context.SaveChangesAsync(CancellationToken.None);
            logger.LogInformation("Saved new message with UID {Uid} as ID {Id}", mimeMessage.MessageId, mailMessage.Id);
        }

        token.ThrowIfCancellationRequested();
        
        var emlPath = GetEmlPath(mailMessage);
        var parentFolder = Path.GetDirectoryName(emlPath);
        if (parentFolder is null)
            throw new InvalidOperationException($"Could not extract parent directory from {emlPath}");

        Directory.CreateDirectory(parentFolder);
        logger.LogInformation("Saving message file with UID {Uid} to eml path {Path}", mimeMessage.MessageId, emlPath);
        await mimeMessage.WriteToAsync(emlPath, CancellationToken.None);
        return true;
    }
}
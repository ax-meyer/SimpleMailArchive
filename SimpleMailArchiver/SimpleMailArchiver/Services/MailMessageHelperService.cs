using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace SimpleMailArchiver.Data;

public class MailMessageHelperService
{
    private readonly ApplicationContext _appContext;
    private readonly IDbContextFactory<ArchiveContext> _dbContextFactory;

    public MailMessageHelperService(ApplicationContext appContext, IDbContextFactory<ArchiveContext> dbContextFactory)
    {
        _appContext = appContext;
        _dbContextFactory = dbContextFactory;
    }

    public string GetEmlPath(MailMessage message)
    {
        return (_appContext.PathConfig.ArchiveBasePath + "/" + message.Folder + "/message-" + message.Id + ".eml")
            .Replace("//", "/");
    }

    public static async Task<string> CreateMailHash(MailMessage message, CancellationToken token = default)
    {
        return await CreateMailHash(message.Date, message.Subject, message.Sender, message.Recipient,
            message.CcRecipient, message.BccRecipient, token);
    }
    
    public static async Task<string> CreateMailHash(DateTime date, string subject, string sender, string recipient, string? ccRecipient, string? bccRecipient, CancellationToken token = default)
    {
        var strData = date.ToString("dd.MM.yyyy-HH:mm:ss");
        strData += subject;
        strData += sender;
        strData += recipient;
        strData += ccRecipient;
        strData += bccRecipient;

        byte[] encodedMessage = Encoding.UTF8.GetBytes(strData);
        using var alg = SHA256.Create();
        string hex = "";

        var hashValue = await alg.ComputeHashAsync(new MemoryStream(encodedMessage), token);
        foreach (byte x in hashValue)
        {
            hex += $"{x:x2}";
        }

        return hex;
    }

    public async Task<bool> SaveMessage(MimeMessage mimeMessage, string folder, CancellationToken token = default)
    {
        var mailMessage = await MailParser.Construct(mimeMessage, folder, token);

        await using var context = await _dbContextFactory.CreateDbContextAsync(token);
        if (await context.MailMessages.AnyAsync(o => o.Hash == mailMessage.Hash, token))
            return false;

        token.ThrowIfCancellationRequested();

        await context.AddAsync(mailMessage, token);

        var emlPath = GetEmlPath(mailMessage);
        var parentFolder = Path.GetDirectoryName(emlPath);
        if (parentFolder is null)
            throw new InvalidOperationException($"Could not extract parent directory from {emlPath}");

        Directory.CreateDirectory(parentFolder);
        await mimeMessage.WriteToAsync(emlPath, token);
        await context.SaveChangesAsync(token);
        return true;
    }
}
using Microsoft.EntityFrameworkCore;

namespace SimpleMailArchiver.Data;

public class DatabaseService
{
    public static void Initialize(IServiceProvider serviceProvider)
    {

        using ArchiveContext context = new(serviceProvider.GetRequiredService<DbContextOptions<ArchiveContext>>());
        context.Database.EnsureCreated();
        
        if (!context.MailMessages.Any())
        {
            context.MailMessages.AddRange(
                new MailMessage
                {
                    Subject = "Test 1",
                    Sender = "Person 1",
                    Hash = "abc",
                    Recipient = "abc",
                    Date = DateTime.Now,
                    Folder = "test",
                    TextBody = "test",
                    HtmlBody = "test"
                },

                new MailMessage
                {
                    Subject = "Test 2",
                    Sender = "Person 2",
                    Hash = "abc",
                    Recipient = "abc",
                    Date = DateTime.Now,
                    Folder = "test",
                    TextBody = "test",
                    HtmlBody = "test"
                },

                new MailMessage
                {
                    Subject = "Test 3",
                    Sender = "Person 3",
                    Hash = "abc",
                    Recipient = "abc",
                    Date = DateTime.Now,
                    Folder = "test",
                    TextBody = "test",
                    HtmlBody = "test"
                }
            );
        }
    
        context.SaveChanges();
    }
}
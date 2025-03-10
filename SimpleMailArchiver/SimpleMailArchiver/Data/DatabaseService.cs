using Microsoft.EntityFrameworkCore;

namespace SimpleMailArchiver.Data
{
    public class DatabaseService
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using ArchiveContext context = new(serviceProvider.GetRequiredService<DbContextOptions<ArchiveContext>>());
            if (Environment.GetEnvironmentVariable("SMA_DELETE_DATABASE") == "true")
            {
                /*
                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();
                context.MailMessages.AddRange(
                    new MailMessage
                    {
                        Subject = "Test 1",
                        Sender = "Person 1",
                        Date = DateTime.Now,
                    },
                    new MailMessage
                    {
                        Subject = "Test 2",
                        Sender = "Person 2",
                        Date = DateTime.Now,
                    },
                    new MailMessage
                    {
                        Subject = "Test 3",
                        Sender = "Person 3",
                        Date = DateTime.Now,
                    }
                );
                context.SaveChanges();
                */
            }
            else
            {
                context.Database.Migrate();
            }
        }
    }
}
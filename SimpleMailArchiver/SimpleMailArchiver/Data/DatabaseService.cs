using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace SimpleMailArchiver.Data
{
    public class DatabaseService
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {

            using (var context = new ArchiveContext(
                serviceProvider.GetRequiredService<
                    DbContextOptions<ArchiveContext>>()))
            {
                context.Database.EnsureCreated();
                // Look for any movies.
                if (!context.MailMessages.Any())
                {


                    context.MailMessages.AddRange(
                        new MailMessage
                        {
                            Subject = "Test 1",
                            Sender = "Person 1",
                            ReceiveTime = DateTime.Now,
                        },

                        new MailMessage
                        {
                            Subject = "Test 2",
                            Sender = "Person 2",
                            ReceiveTime = DateTime.Now,
                        },

                        new MailMessage
                        {
                            Subject = "Test 3",
                            Sender = "Person 3",
                            ReceiveTime = DateTime.Now,
                        }
                    );
                }

                context.SaveChanges();
                //context.Devices.Where(x => x.Name == "Küche").FirstOrDefault().Images = context.Images.ToList();
                context.SaveChanges();
            }
        }
    }
}


using System;
namespace SimpleMailArchiver.Data
{
	public static class Cleanup
	{
		public static async Task RecalculateHashes(CancellationToken token = default)
        {
			var context = await Program.ContextFactory.CreateDbContextAsync().ConfigureAwait(false);
			int count = 0;
			foreach(var message in context.MailMessages)
            {
				message.Hash = await ParseMailMessage.CreateMailHash(message, token);
			if (count++ % 100 == 0)
				await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

		public static async Task RemoveDuplicates()
        {
            var context = await Program.ContextFactory.CreateDbContextAsync().ConfigureAwait(false);
			var duplicates = context.MailMessages.GroupBy(i => i.Hash)
                     .Where(x => x.Count() > 1)
                     .Select(val => val.Key);

			foreach(var dupHash in duplicates)
			{
				var msgsEqHash = context.MailMessages.Where(m => m.Hash == dupHash);
				var refMsg = msgsEqHash.First();
				bool start = true;
				foreach (var msg in msgsEqHash)
                {
					if (start)
                    {
						start = false;
						continue;
                    }

					if (refMsg.Equals(msg))
                    {
						File.Delete(msg.EmlPath);
						context.MailMessages.Remove(msg);
					}
                }
			}
			await context.SaveChangesAsync().ConfigureAwait(false);
        }
	}
}


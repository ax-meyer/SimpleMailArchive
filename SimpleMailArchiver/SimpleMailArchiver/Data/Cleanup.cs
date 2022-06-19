namespace SimpleMailArchiver.Data
{
	public static class Cleanup
	{
        /// <summary>
        /// WARNING: NOT TESTED YET
        /// </summary>
        /// <returns></returns>
        private static async Task RecalculateHashes(CancellationToken token = default)
        {
			using var context = await Program.ContextFactory.CreateDbContextAsync().ConfigureAwait(false);
			int count = 0;
			foreach(var message in context.MailMessages)
            {
				message.Hash = await Utils.CreateMailHash(message, token);
			if (count++ % 100 == 0)
				await context.SaveChangesAsync().ConfigureAwait(false);
            }
        }

		/// <summary>
        /// WARNING: NOT TESTED YET
        /// </summary>
        /// <returns></returns>
		private static async Task RemoveDuplicates()
        {
            using var context = await Program.ContextFactory.CreateDbContextAsync().ConfigureAwait(false);
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


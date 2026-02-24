using FluentResults;
using Microsoft.EntityFrameworkCore;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services;

public class DeleteService(
    IDbContextFactory<ArchiveContext> dbContextFactory,
    MailMessageHelperService messageHelperService,
    ILogger<DeleteService> logger)
{
    /// <summary>
    /// Deletes a mail message from both the database and the file system.
    /// </summary>
    /// <param name="messageId">The ID of the message to delete</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>Result indicating success or failure with error messages</returns>
    public async Task<Result> DeleteMessageAsync(long messageId, CancellationToken token = default)
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(token);

            // Find the message
            var message = await context.MailMessages.FirstOrDefaultAsync(m => m.Id == messageId, token);
            if (message is null)
            {
                var errorMsg = $"Message with ID {messageId} not found";
                logger.LogWarning("Message not found for deletion: {MessageId}", messageId);
                return Result.Fail(errorMsg);
            }

            await using var transaction = await context.Database.BeginTransactionAsync(token);

            try
            {
                // Get the EML file path
                var emlPath = messageHelperService.GetEmlPath(message);

                // Delete the EML file if it exists
                if (!File.Exists(emlPath))
                {
                    logger.LogWarning("EML file not found for message {MessageId}, path {Path}", messageId, emlPath);
                    return Result.Fail($"EML file not found for message {messageId}: {emlPath}");
                }
                else
                {
                    File.Delete(emlPath);
                    logger.LogInformation("Deleted EML file for message {MessageId} at path {Path}", messageId,
                        emlPath);
                }

                // Delete the message from the database
                context.MailMessages.Remove(message);
                await context.SaveChangesAsync(token);

                await transaction.CommitAsync(token);
                logger.LogInformation("Successfully deleted message {MessageId} from database and file system",
                    messageId);
                return Result.Ok();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(token);
                var errorMsg = $"Error during message deletion: {ex.Message}";
                logger.LogError(ex, "Error during message deletion, transaction rolled back for message {MessageId}",
                    messageId);
                return Result.Fail(errorMsg);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Failed to delete message {messageId}: {ex.Message}";
            logger.LogError(ex, "Failed to delete message {MessageId}: {ExceptionMessage}", messageId, ex.Message);
            return Result.Fail(errorMsg);
        }
    }
}
using FluentResults;
using Microsoft.EntityFrameworkCore;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver.Services;

public class MoveService(
    IDbContextFactory<ArchiveContext> dbContextFactory,
    MailMessageHelperService messageHelperService,
    ILogger<MoveService> logger)
{
    /// <summary>
    /// Moves a single mail message to a different folder in both the database and file system.
    /// </summary>
    /// <param name="messageId">The ID of the message to move</param>
    /// <param name="targetFolder">The target folder name</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>Result indicating success or failure with error messages</returns>
    public async Task<Result> MoveMessageAsync(long messageId, string targetFolder, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(targetFolder);
        if (string.IsNullOrWhiteSpace(targetFolder))
            return Result.Fail("Target folder cannot be empty");

        await using var context = await dbContextFactory.CreateDbContextAsync(token);
        await using var transaction = await context.Database.BeginTransactionAsync(token);

        try
        {
            // Find the message
            var message = await context.MailMessages.FirstOrDefaultAsync(m => m.Id == messageId, token);
            if (message is null)
            {
                var errorMsg = $"Message with ID {messageId} not found";
                logger.LogWarning("Message not found for move: {MessageId}", messageId);
                return Result.Fail(errorMsg);
            }

            // Check if message is already in target folder
            if (message.Folder == targetFolder)
            {
                logger.LogInformation("Message {MessageId} is already in folder {TargetFolder}", messageId,
                    targetFolder);
                return Result.Ok();
            }

            // Get the old EML file path
            var oldEmlPath = messageHelperService.GetEmlPath(message);
            var oldFolder = message.Folder;

            // Verify the EML file exists
            if (!File.Exists(oldEmlPath))
            {
                await transaction.RollbackAsync(token);
                logger.LogWarning("EML file not found for message {MessageId}, path {Path}", messageId, oldEmlPath);
                return Result.Fail($"EML file not found for message {messageId}: {oldEmlPath}");
            }

            message.Folder = targetFolder;

            // Create target directory if it doesn't exist
            var newEmlPath = messageHelperService.GetEmlPath(message);
            var targetDirPath = Path.GetDirectoryName(newEmlPath) ??
                                throw new InvalidOperationException("Could not determine target directory path");
            if (!Directory.Exists(targetDirPath))
            {
                Directory.CreateDirectory(targetDirPath);
                logger.LogInformation("Created new folder directory: {TargetPath}", targetDirPath);
            }
            
            // Move the EML file
            File.Move(oldEmlPath, newEmlPath, overwrite: false);
            logger.LogInformation(
                "Moved EML file for message {MessageId} from {OldPath} to {NewPath}",
                messageId, oldEmlPath, newEmlPath);

            await context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            logger.LogInformation(
                "Successfully moved message {MessageId} from folder {OldFolder} to {NewFolder}",
                messageId, oldFolder, targetFolder);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(token);
            var errorMsg = $"Failed to move message {messageId}: {ex.Message}";
            logger.LogError(ex, "Failed to move message {MessageId}: {ExceptionMessage}", messageId, ex.Message);

            return Result.Fail(errorMsg);
        }
    }
    
    /// <summary>
    /// Gets all distinct folders currently in the database.
    /// </summary>
    /// <param name="token">Cancellation token</param>
    /// <returns>List of folder names</returns>
    public async Task<List<string>> GetAvailableFoldersAsync(CancellationToken token = default)
    {
        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(token);
            var folders = await context.MailMessages
                .Select(m => m.Folder)
                .Distinct()
                .OrderBy(f => f)
                .ToListAsync(token);

            logger.LogDebug("Retrieved {FolderCount} available folders", folders.Count);
            return folders;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve available folders");
            return new List<string>();
        }
    }

}
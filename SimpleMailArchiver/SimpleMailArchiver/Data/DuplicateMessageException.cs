namespace SimpleMailArchiver.Data;

public class DuplicateMessageException : Exception
{
    public DuplicateMessageException()
    {
    }

    public DuplicateMessageException(string message) : base(message)
    {
    }
}
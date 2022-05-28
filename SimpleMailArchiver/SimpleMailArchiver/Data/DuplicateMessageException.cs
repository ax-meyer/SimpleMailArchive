using System;
namespace SimpleMailArchiver.Data
{
	public class DuplicateMessageException : Exception
	{
		public DuplicateMessageException() : base()
		{
		}

		public DuplicateMessageException(string message) : base(message)
		{ 
        }
	}
}


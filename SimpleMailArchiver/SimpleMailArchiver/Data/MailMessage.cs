using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable disable

namespace SimpleMailArchiver.Data
{
	public partial class MailMessage
	{
		[Key]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long ID { get; set; }
		public string Hash { get; set; }
		public string Subject { get; set; }
		public string Sender { get; set; }
		public string Recipient { get; set; }
		public string CC_recipient { get; set; }
		public string BCC_recipient { get; set; }
		public DateTime ReceiveTime { get; set; }
		public string Attachments { get; set; }
		public string Folder { get; set; }
		public string Message { get; set; }
	}
}


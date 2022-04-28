using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
#nullable disable

namespace SimpleMailArchiver.Data
{
	public partial class ArchiveContext : DbContext
    {
        /*public signageContext()
        {
        }*/

        public ArchiveContext(DbContextOptions<ArchiveContext> options)
            : base(options)
        {
        }

        public virtual DbSet<MailMessage> MailMessages { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {

            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MailMessage>(entity =>
            {
                entity.ToTable("MailMessages");
                entity.Property(e => e.ID).HasColumnName("ID");
                entity.Property(e => e.Attachments).HasColumnName("ATTACHMENTS");
                entity.Property(e => e.Recipient).HasColumnName("RECIPIENT");
                entity.Property(e => e.BCC_recipient).HasColumnName("BCC_RECIPIENT");
                entity.Property(e => e.CC_recipient).HasColumnName("CC_RECIPIENT");
                entity.Property(e => e.Folder).HasColumnName("FOLDER");
                entity.Property(e => e.Hash).HasColumnName("HASH");
                entity.Property(e => e.Message).HasColumnName("MESSAGE");
                entity.Property(e => e.ReceiveTime).HasColumnName("RECEIVE_TIME");
                entity.Property(e => e.Sender).HasColumnName("SENDER");
                entity.Property(e => e.Subject).HasColumnName("SUBJECT");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}


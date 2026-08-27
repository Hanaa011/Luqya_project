using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using LostFound.Conversations;
using LostFound.Reports;

namespace LostFound.EntityFrameworkCore
{
    public static class ConversationConfiguration
    {
        public static void ConfigureConversation(this ModelBuilder builder)
        {
            builder.Entity<Conversation>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "Conversations", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();

                b.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Restrict).IsRequired();

                b.HasMany(x => x.Messages)
                    .WithOne()
                    .HasForeignKey(x => x.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();

                // Duplicate prevention (ReportId + same two users) -
                // participant order is normalized before insert, so this
                // unique index catches a real duplicate regardless of who
                // started the conversation.
                b.HasIndex(x => new { x.ReportId, x.Participant1Id, x.Participant2Id }).IsUnique();
            });

            builder.Entity<ConversationMessage>(b =>
            {
                b.ToTable(LostFoundDbProperties.DbTablePrefix + "ConversationMessages", LostFoundDbProperties.DbSchema);
                b.ConfigureByConvention();

                b.Property(x => x.Text).HasMaxLength(2000).IsRequired();

                b.HasIndex(x => x.ConversationId);
            });
        }
    }
}

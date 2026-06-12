using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<TreatmentSchema> TreatmentSchemas => Set<TreatmentSchema>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<EmailOutbox> EmailOutbox => Set<EmailOutbox>();
    public DbSet<MagicLink> MagicLinks => Set<MagicLink>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<ReminderTemplate> ReminderTemplates => Set<ReminderTemplate>();
    public DbSet<IntakeReport> IntakeReports => Set<IntakeReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<UserCredential>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CredentialId).IsUnique();
            entity.HasOne(e => e.User).WithMany(u => u.Credentials).HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasOne(e => e.User).WithOne(u => u.Settings).HasForeignKey<UserSetting>(e => e.UserId);
            entity.Property(e => e.SendPhotos).HasDefaultValue(true);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasOne(e => e.User).WithMany(u => u.Sessions).HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<EmailOutbox>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Status, e.NextAttemptAt });
            if (Database.IsNpgsql())
            {
                entity.Property(e => e.AttachmentsJson).HasColumnType("jsonb");
            }
            entity.HasOne(e => e.User)
                  .WithMany(u => u.EmailOutboxItems)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MagicLink>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
        });

        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Endpoint).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReminderTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            if (Database.IsNpgsql())
            {
                entity.Property(e => e.Periods).HasColumnType("jsonb");
            }
            entity.HasOne(e => e.Schema)
                  .WithMany()
                  .HasForeignKey(e => e.SchemaId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.ReminderTemplates)
                  .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<IntakeReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => new { e.UserId, e.TemplateId, e.Period, e.Date }).IsUnique();
            entity.HasOne(e => e.Template)
                  .WithMany()
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.IntakeReports)
                  .HasForeignKey(e => e.UserId);
        });

        modelBuilder.Entity<Measurement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(e => e.User).WithMany(u => u.Measurements).HasForeignKey(e => e.UserId);

            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Measurement_Sys", "\"Sys\" > 40 AND \"Sys\" < 300");
                t.HasCheckConstraint("CK_Measurement_Dia", "\"Dia\" > 20 AND \"Dia\" < 200");
                t.HasCheckConstraint("CK_Measurement_Pulse", "\"Pulse\" > 30 AND \"Pulse\" < 250");
            });
        });

        modelBuilder.Entity<TreatmentSchema>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            if (Database.IsNpgsql())
            {
                entity.Property(e => e.ScheduleDocument).HasColumnType("jsonb");
            }
            entity.HasOne(e => e.User)
                  .WithMany(u => u.TreatmentSchemas)
                  .HasForeignKey(e => e.UserId);
        });
    }
}

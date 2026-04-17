using BpTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BpTracker.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<TreatmentSchema> TreatmentSchemas => Set<TreatmentSchema>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Measurement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.HasCheckConstraint("CK_Measurement_Sys", "\"Sys\" > 40 AND \"Sys\" < 300");
            entity.HasCheckConstraint("CK_Measurement_Dia", "\"Dia\" > 20 AND \"Dia\" < 200");
            entity.HasCheckConstraint("CK_Measurement_Pulse", "\"Pulse\" > 30 AND \"Pulse\" < 250");
        });

        modelBuilder.Entity<TreatmentSchema>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.ScheduleDocument).HasColumnType("jsonb");
        });
    }
}

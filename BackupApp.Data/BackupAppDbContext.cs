using BackupApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BackupApp.Data;

public class BackupAppDbContext : DbContext
{
    public BackupAppDbContext(DbContextOptions<BackupAppDbContext> options) : base(options)
    {
    }

    public DbSet<BackupTask> BackupTasks => Set<BackupTask>();
    public DbSet<BackupHistory> BackupHistories => Set<BackupHistory>();
    public DbSet<BackupSetting> BackupSettings => Set<BackupSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureBackupTask(modelBuilder);
        ConfigureBackupHistory(modelBuilder);
        ConfigureBackupSetting(modelBuilder);
    }

    private static void ConfigureBackupTask(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BackupTask>();

        entity.ToTable("BackupTasks");
        entity.HasKey(t => t.Id);

        entity.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(t => t.SourcePath)
            .IsRequired()
            .HasMaxLength(1000);

        entity.Property(t => t.TargetPath)
            .IsRequired()
            .HasMaxLength(1000);

        entity.Property(t => t.ScheduleType)
            .IsRequired();

        entity.Property(t => t.ScheduleTime)
            .HasMaxLength(10);

        entity.Property(t => t.ScheduleDays)
            .HasMaxLength(100);

        entity.Property(t => t.CreatedDate)
            .IsRequired();

        entity.Property(t => t.IsEnabled)
            .IsRequired();

        entity.Property(t => t.BackupType)
            .IsRequired();

        entity.Property(t => t.UseCompression)
            .IsRequired();

        entity.Property(t => t.CompressionLevel)
            .IsRequired();

        entity.Property(t => t.LastBackupTime);
        entity.Property(t => t.LastFullBackupTime);
        entity.Property(t => t.UseEncryption)
            .IsRequired();
        entity.Property(t => t.EncryptionPasswordHash)
            .HasMaxLength(500);
        entity.Property(t => t.RetentionPolicyType)
            .IsRequired();
        entity.Property(t => t.MaxVersions);
        entity.Property(t => t.MaxAgeDays);
    }

    private static void ConfigureBackupHistory(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BackupHistory>();

        entity.ToTable("BackupHistories");
        entity.HasKey(h => h.Id);

        entity.Property(h => h.Status)
            .IsRequired();

        entity.Property(h => h.BackupType)
            .IsRequired();

        entity.Property(h => h.UsedCompression)
            .IsRequired();

        entity.Property(h => h.CompressionLevel)
            .IsRequired();

        entity.Property(h => h.OutputArtifactPath)
            .HasMaxLength(1000);

        entity.Property(h => h.CompressedSize)
            .IsRequired();

        entity.Property(h => h.Duration)
            .IsRequired();

        entity.HasOne(h => h.Task)
            .WithMany(t => t.History)
            .HasForeignKey(h => h.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureBackupSetting(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BackupSetting>();

        entity.ToTable("BackupSettings");
        entity.HasKey(s => s.Id);

        entity.Property(s => s.Key)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(s => s.Value)
            .IsRequired()
            .HasMaxLength(2000);
    }
}

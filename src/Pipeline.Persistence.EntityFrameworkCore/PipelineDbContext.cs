using Microsoft.EntityFrameworkCore;

using Pipeline.Persistence.EntityFrameworkCore.Entities;

namespace Pipeline.Persistence.EntityFrameworkCore;

/// <summary>
/// Maps pipeline runs, specifications, jobs, steps, and logs, including concurrency tokens and cascade deletion.
/// </summary>
/// <remarks>
/// When registered through UseSqlServer, the persistence library applies its bundled migrations during host startup before hosted workers start.
/// Stores obtain short-lived contexts through <see cref="IDbContextFactory{TContext}" />.
/// </remarks>
/// <param name="options">The pipeline registration options or typed database context options to use.</param>
public sealed class PipelineDbContext(DbContextOptions<PipelineDbContext> options) : DbContext(options)
{
    internal DbSet<PipelineSpecificationEntity> Specifications => Set<PipelineSpecificationEntity>();
    internal DbSet<PipelineJobEntity> Jobs => Set<PipelineJobEntity>();

    internal DbSet<PipelineStepEntity> Steps => Set<PipelineStepEntity>();

    internal DbSet<PipelineRunEntity> Runs => Set<PipelineRunEntity>();

    internal DbSet<PipelineLogEntryEntity> Logs => Set<PipelineLogEntryEntity>();


    /// <summary>
    /// Configures pipeline tables, relationships, indexes, concurrency revisions, and cascade deletion.
    /// </summary>
    /// <param name="modelBuilder">The builder used to configure the persistence model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("pipeline");

        /* ============= Specifications ================= */

        var specifications =
            modelBuilder.Entity<PipelineSpecificationEntity>();

        specifications.ToTable("PipelineSpecifications");

        specifications.HasKey(specification => specification.RunId);

        specifications.Property(specification => specification.RunId)
            .ValueGeneratedNever();

        specifications.Property(specification => specification.DefinitionId)
            .HasMaxLength(200)
            .IsRequired();

        specifications.Property(specification => specification.InputJson)
            .IsRequired();

        specifications.Property(specification => specification.SettingsJson)
            .IsRequired();

        specifications.HasOne<PipelineRunEntity>()
            .WithOne()
            .HasForeignKey<PipelineSpecificationEntity>(
                specification => specification.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        /* ============= Jobs ================= */
        var jobs = modelBuilder.Entity<PipelineJobEntity>();

        jobs.ToTable("PipelineJobs");

        jobs.HasKey(job => new
        {
            job.RunId,
            job.JobId
        });

        jobs.Property(job => job.JobId)
            .HasMaxLength(200)
            .IsRequired();

        jobs.Property(job => job.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        jobs.Property(job => job.Revision)
            .IsConcurrencyToken();

        jobs.HasOne<PipelineRunEntity>()
            .WithMany()
            .HasForeignKey(job => job.RunId)
            .OnDelete(DeleteBehavior.Cascade);




        /* ============= Steps ================= */

        var steps = modelBuilder.Entity<PipelineStepEntity>();

        steps.ToTable("PipelineSteps");

        steps.HasKey(step => new
        {
            step.RunId,
            step.JobId,
            step.StepId
        });

        steps.Property(step => step.JobId)
            .HasMaxLength(200);

        steps.Property(step => step.StepId)
            .HasMaxLength(200);

        steps.Property(step => step.Revision)
            .IsConcurrencyToken();

        steps.Property(step => step.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        steps.HasIndex(step => new
        {
            step.Status,
            step.NextAttemptAt
        });

        steps.HasOne<PipelineJobEntity>()
            .WithMany()
            .HasForeignKey(step => new
            {
                step.RunId,
                step.JobId
            })
            .OnDelete(DeleteBehavior.Cascade);




        /* ============= Runs ================= */
        var runs = modelBuilder.Entity<PipelineRunEntity>();

        runs.ToTable("PipelineRuns");

        runs.HasKey(run => run.Id);

        // The runtime already assigns this ID.
        runs.Property(run => run.Id)
            .ValueGeneratedNever();

        runs.Property(run => run.DefinitionId)
            .HasMaxLength(200)
            .IsRequired();

        runs.Property(run => run.DefinitionDisplayName)
            .HasMaxLength(300)
            .IsRequired();

        runs.Property(run => run.Title)
            .HasMaxLength(1000)
            .IsRequired();

        runs.Property(run => run.CreatedBy)
            .HasMaxLength(300)
            .IsRequired();

        runs.Property(run => run.Revision)
            .IsConcurrencyToken();

        runs.Property(run => run.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        runs.Property(run => run.StatusText)
            .IsRequired();

        runs.HasIndex(run => new
        {
            run.CreatedAt,
            run.Id
        });

        runs.HasIndex(run => new
        {
            run.Status,
            run.QueuedAt,
            run.Id
        });


        /* ============= Logs ================= */
        var logs = modelBuilder.Entity<PipelineLogEntryEntity>();

        logs.ToTable("PipelineRunLogs");

        logs.HasKey(log => new
        {
            log.RunId,
            log.Sequence
        });

        logs.Property(log => log.Sequence)
            .ValueGeneratedNever();

        logs.Property(log => log.Message)
            .IsRequired();

        logs.Property(log => log.Level)
            .HasConversion<int>()
            .HasDefaultValue(global::Pipeline.Runtime.PipelineLogLevel.Information)
            .IsRequired();

        logs.Property(log => log.JobId)
            .HasMaxLength(200);

        logs.Property(log => log.StepId)
            .HasMaxLength(200);

        runs.HasMany(run => run.Logs)
            .WithOne()
            .HasForeignKey(log => log.RunId)
            .OnDelete(DeleteBehavior.Cascade);


    }

}

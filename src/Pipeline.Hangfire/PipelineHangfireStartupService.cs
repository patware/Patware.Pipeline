using global::Hangfire;

using Microsoft.Extensions.Hosting;

namespace Pipeline.Hangfire;

/// <summary>
/// Schedules immediate recovery and a recurring minutely recovery job when the host starts.
/// </summary>
/// <param name="recurringJobs">The Hangfire manager registering recurring recovery work.</param>
/// <param name="backgroundJobs">The Hangfire client used to enqueue or schedule work.</param>
public sealed class PipelineHangfireStartupService(
    IRecurringJobManager recurringJobs,
    IBackgroundJobClient backgroundJobs) : IHostedService
{
    /// <summary>
    /// Registers minutely recovery and dispatches an immediate recovery scan.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A completed task after the recovery jobs have been registered.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        recurringJobs.AddOrUpdate<PipelineRecoveryJob>(
            "pipeline-recovery",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Minutely());

        cancellationToken.ThrowIfCancellationRequested();

        backgroundJobs.Enqueue<PipelineRecoveryJob>(
            job => job.ExecuteAsync(CancellationToken.None));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Completes shutdown of the startup service; Hangfire manages its own worker lifetime.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel this operation.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
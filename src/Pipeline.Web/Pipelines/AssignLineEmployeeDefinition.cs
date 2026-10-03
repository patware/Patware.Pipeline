using Pipeline.Core;
using Pipeline.Web.Pipelines.Actions;

namespace Pipeline.Web.Pipelines;

public sealed record AssignLineInput(string Upn, ulong PhoneNumber);

public sealed record AssignLineEmployeeSettings(string CallingPolicy, string PhoneSystemLicense);

public sealed class AssignLineEmployeeDefinition(IPipelineBuilderFactory builders)
{
    public static PipelineDefinition Definition { get; } = new(
        Id: "AssignLineOrgEmployee",
        DisplayName: "Assign employee phone line",
        Version: 1);


    public PipelineRequest Build(
        string upn,
        ulong phoneNumber,
        string createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(upn);

        var input = new AssignLineInput(upn, phoneNumber);

        // Later, obtain these from IOptions and snapshot them here.
        var settings = new AssignLineEmployeeSettings(
            CallingPolicy: "Precinct All Users Policy",
            PhoneSystemLicense: "MCOEV");

        return PipelineRequest.Create(
            definition: Definition,
            title: $"Assign phone number {phoneNumber} to {upn}",
            createdBy: createdBy,
            input: input,
            settings: settings,
            plan: CreatePlan(input, settings));
    }

    public PipelinePlan CreatePlan(AssignLineInput input, AssignLineEmployeeSettings settings)
    {
        var pipeline = builders.Create(Definition);

        var prepJob = pipeline.AddJob("prepJob");

        var preparationStep = prepJob
            .Step<PrepareEmployee>("prepare-employee")
            .Produces((step, ct) => step.ExecuteAsync(input.Upn, ct));

        var waitForSyncJob = pipeline
            .AddJob("wait-for-directory-sync")
            .After(prepJob)
            .When(preparationStep, result => result.RequiresDirectorySync);

        waitForSyncJob
            .Poll<CheckPhoneSystemLicense>("check-phone-system-license")
            .Check(
                (step, ct) => step.ExecuteAsync(input.Upn, settings.PhoneSystemLicense, ct),
                every: TimeSpan.FromMinutes(1),
                timeout: TimeSpan.FromMinutes(30)
            );

        var assignLineJob = pipeline
            .AddJob("assign-line")
            .After(waitForSyncJob, allowConditionSkipped: true);

        assignLineJob
            .Step<AssignPhoneNumber>("assign-phone-number")
            .Execute((step, ct) => step.ExecuteAsync(input.Upn, input.PhoneNumber, ct))
            .Step<AssignCallingPolicy>("assign-calling-policy")
            .Execute((step, ct) =>
                step.ExecuteAsync(input.Upn, settings.CallingPolicy, ct))
            .Poll<CheckEnterpriseVoiceEnabled>("check-enterprise-voice")
            .Check(
                (step, ct) => step.ExecuteAsync(input.Upn, ct),
                every: TimeSpan.FromSeconds(5),
                timeout: TimeSpan.FromMinutes(10));

        return pipeline.Build();
    }
}

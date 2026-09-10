namespace ServerGuard.Tools.Probing;

/// <summary>Bir kontrolün adı, sonucu ve insan tarafından okunabilir açıklaması.</summary>
public sealed record ProbeStepResult(string Name, ProbeOutcome Outcome, string Detail)
{
    public static ProbeStepResult Pass(string name, string detail) => new(name, ProbeOutcome.Passed, detail);

    public static ProbeStepResult Warn(string name, string detail) => new(name, ProbeOutcome.Warning, detail);

    public static ProbeStepResult Fail(string name, string detail) => new(name, ProbeOutcome.Failed, detail);

    public static ProbeStepResult Skip(string name, string detail) => new(name, ProbeOutcome.Skipped, detail);
}

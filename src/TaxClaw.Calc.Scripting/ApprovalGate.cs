namespace TaxClaw.Calc.Scripting;

/// <summary>
/// Records which generated sources a human has approved, by hash. Compilation/registration of a
/// function is gated on approval, so no unapproved code can be promoted into the engine.
/// </summary>
public sealed class ApprovalGate
{
    private readonly HashSet<string> _approvedHashes = new();

    public void Approve(FunctionSource source) => _approvedHashes.Add(source.Hash);

    public bool IsApproved(FunctionSource source) => _approvedHashes.Contains(source.Hash);
}

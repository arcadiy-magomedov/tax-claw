namespace TaxClaw.Calc.Functions;

/// <summary>
/// Holds approved calc functions keyed by line + version. The engine only runs functions that
/// were registered here, so version pinning is enforced at lookup time.
/// </summary>
public sealed class CalcFunctionRegistry
{
    private readonly Dictionary<CalcFunctionKey, ICalcFunction> _functions = new();

    public void Register(CalcFunctionKey key, ICalcFunction function) =>
        _functions[key] = function;

    public bool TryResolve(CalcFunctionKey key, out ICalcFunction? function) =>
        _functions.TryGetValue(key, out function);
}

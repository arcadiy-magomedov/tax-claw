using TaxClaw.Core.Model;

namespace TaxClaw.Export;

/// <summary>Projects the canonical return into an output format. Each format is one implementation.</summary>
public interface IReturnExporter<out T>
{
    T Export(TaxReturn taxReturn);
}

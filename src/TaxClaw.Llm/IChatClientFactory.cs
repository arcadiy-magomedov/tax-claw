using Microsoft.Extensions.AI;

namespace TaxClaw.Llm;

public interface IChatClientFactory
{
    IChatClient Create();

    /// <summary>Returns a model catalog for providers that can enumerate models, else null.</summary>
    IModelCatalog? CreateCatalog();
}

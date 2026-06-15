using Microsoft.Extensions.AI;

namespace TaxClaw.Llm;

public interface IChatClientFactory
{
    IChatClient Create();
}

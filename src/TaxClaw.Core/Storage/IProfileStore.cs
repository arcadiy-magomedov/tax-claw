using TaxClaw.Core.Model;

namespace TaxClaw.Core.Storage;

public interface IProfileStore
{
    Task<Profile?> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(Profile profile, CancellationToken ct = default);
}

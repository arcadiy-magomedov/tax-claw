using TaxClaw.Core.Model;

namespace TaxClaw.Core.Storage;

public interface IPreferencesStore
{
    Task<Preferences?> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(Preferences preferences, CancellationToken ct = default);
}

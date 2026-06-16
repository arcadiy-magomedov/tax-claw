using TaxClaw.Core.Model;

namespace TaxClaw.Core.Storage;

public interface IProjectStore
{
    Task<Project> CreateAsync(TaxYear year, Profile profileSnapshot, CancellationToken ct = default);
    Task<Project?> LoadAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default);
}

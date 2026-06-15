using System.Text.Json;
using TaxClaw.Core.Model;
using TaxClaw.Core.Storage;

namespace TaxClaw.Storage;

public sealed class JsonProfileStore(StorageRoot root) : IProfileStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public async Task<Profile?> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(root.ProfileFile))
        {
            return null;
        }

        await using var stream = File.OpenRead(root.ProfileFile);
        return await JsonSerializer.DeserializeAsync<Profile>(stream, Json, ct);
    }

    public async Task SaveAsync(Profile profile, CancellationToken ct = default)
    {
        Directory.CreateDirectory(root.Path);
        await using var stream = File.Create(root.ProfileFile);
        await JsonSerializer.SerializeAsync(stream, profile, Json, ct);
    }
}

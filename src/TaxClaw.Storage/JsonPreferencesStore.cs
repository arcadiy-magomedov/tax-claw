using System.Text.Json;
using TaxClaw.Core.Model;
using TaxClaw.Core.Storage;

namespace TaxClaw.Storage;

public sealed class JsonPreferencesStore(StorageRoot root) : IPreferencesStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public async Task<Preferences?> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(root.PreferencesFile))
        {
            return null;
        }

        await using var stream = File.OpenRead(root.PreferencesFile);
        return await JsonSerializer.DeserializeAsync<Preferences>(stream, Json, ct);
    }

    public async Task SaveAsync(Preferences preferences, CancellationToken ct = default)
    {
        Directory.CreateDirectory(root.Path);
        await using var stream = File.Create(root.PreferencesFile);
        await JsonSerializer.SerializeAsync(stream, preferences, Json, ct);
    }
}

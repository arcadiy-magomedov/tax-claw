using System.Text.Json;
using TaxClaw.Core.Model;
using TaxClaw.Core.Storage;

namespace TaxClaw.Storage;

public sealed class JsonProjectStore(StorageRoot root) : IProjectStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public async Task<Project> CreateAsync(TaxYear year, Profile profileSnapshot, CancellationToken ct = default)
    {
        string id = year.ToString();
        string dir = root.ProjectDirectory(id);

        if (Directory.Exists(dir))
        {
            throw new InvalidOperationException($"Project '{id}' already exists.");
        }

        Directory.CreateDirectory(dir);

        var project = new Project
        {
            Id = id,
            Year = year,
            ProfileSnapshot = profileSnapshot,
            Status = ProjectStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await using var stream = File.Create(root.ProjectFile(id));
        await JsonSerializer.SerializeAsync(stream, project, Json, ct);
        return project;
    }

    public async Task<Project?> LoadAsync(string id, CancellationToken ct = default)
    {
        string file = root.ProjectFile(id);
        if (!File.Exists(file))
        {
            return null;
        }

        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<Project>(stream, Json, ct);
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(root.ProjectsDirectory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> ids = Directory
            .GetDirectories(root.ProjectsDirectory)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name)
            .ToList();

        return Task.FromResult(ids);
    }
}

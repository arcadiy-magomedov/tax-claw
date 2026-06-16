using TaxClaw.Core.Model;
using TaxClaw.Storage;
using Xunit;

namespace TaxClaw.Storage.Tests;

public class JsonStoreTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "taxclaw-tests-" + Guid.NewGuid().ToString("N"));

    private StorageRoot Root => new(_root);

    [Fact]
    public async Task Profile_round_trips_through_disk()
    {
        var store = new JsonProfileStore(Root);
        var profile = new Profile { FullName = "Jan Novák", RodneCislo = "900101/1234" };

        await store.SaveAsync(profile);
        var loaded = await store.LoadAsync();

        Assert.Equal(profile, loaded);
    }

    [Fact]
    public async Task Loading_a_missing_profile_returns_null()
    {
        var store = new JsonProfileStore(Root);
        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public async Task Create_then_load_and_list_a_project()
    {
        var store = new JsonProjectStore(Root);
        var profile = new Profile { FullName = "Jan Novák" };

        var created = await store.CreateAsync(TaxYear.Of(2027), profile);

        Assert.Equal("2027", created.Id);
        Assert.Equal(ProjectStatus.Draft, created.Status);

        var loaded = await store.LoadAsync("2027");
        Assert.Equal(created, loaded);

        var ids = await store.ListAsync();
        Assert.Contains("2027", ids);
    }

    [Fact]
    public async Task Creating_a_duplicate_project_throws()
    {
        var store = new JsonProjectStore(Root);
        var profile = new Profile { FullName = "Jan Novák" };

        await store.CreateAsync(TaxYear.Of(2027), profile);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.CreateAsync(TaxYear.Of(2027), profile));
    }

    [Fact]
    public async Task Preferences_round_trip_through_disk()
    {
        var store = new JsonPreferencesStore(Root);
        var prefs = new Preferences
        {
            Provider = "copilot",
            Model = "claude-opus-4.8",
            ReasoningEffort = "high"
        };

        await store.SaveAsync(prefs);
        var loaded = await store.LoadAsync();

        Assert.Equal(prefs, loaded);
    }

    [Fact]
    public async Task Loading_missing_preferences_returns_null()
    {
        var store = new JsonPreferencesStore(Root);
        Assert.Null(await store.LoadAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

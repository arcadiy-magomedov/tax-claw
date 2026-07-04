namespace TaxClaw.Storage;

/// <summary>
/// Resolves on-disk locations under the tax-claw data root. Defaults to <c>~/.tax-claw</c>;
/// tests inject a temp directory.
/// </summary>
public sealed class StorageRoot
{
    public StorageRoot(string? rootPath = null)
    {
        Path = rootPath ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tax-claw");
    }

    public string Path { get; }

    public string ProfileFile => System.IO.Path.Combine(Path, "profile.json");

    public string PreferencesFile => System.IO.Path.Combine(Path, "preferences.json");

    public string ProjectsDirectory => System.IO.Path.Combine(Path, "projects");

    public string ProjectDirectory(string id) => System.IO.Path.Combine(ProjectsDirectory, id);

    public string ProjectFile(string id) => System.IO.Path.Combine(ProjectDirectory(id), "project.json");

    public string SchemasDirectory => System.IO.Path.Combine(Path, "schemas");

    /// <summary>Where the user drops the official EPO XSD for form 25 5405 (DPFDP5).</summary>
    public string EpoXsdFile => System.IO.Path.Combine(SchemasDirectory, "dpfdp5_epo2.xsd");
}

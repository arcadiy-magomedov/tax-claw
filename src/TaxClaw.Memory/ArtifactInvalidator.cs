namespace TaxClaw.Memory;

/// <summary>
/// Selects artifacts still valid for the active versions. Calc functions must match both law and
/// form version; document parsers match on law version only (they do not depend on the form).
/// </summary>
public static class ArtifactInvalidator
{
    public static IReadOnlyList<VersionedArtifact> SelectValid(
        IEnumerable<VersionedArtifact> artifacts, string lawVersion, string formVersion)
    {
        return artifacts.Where(a => IsValid(a, lawVersion, formVersion)).ToList();
    }

    private static bool IsValid(VersionedArtifact artifact, string lawVersion, string formVersion)
    {
        if (artifact.LawVersion != lawVersion)
        {
            return false;
        }

        return artifact.Kind switch
        {
            ArtifactKind.CalcFunction => artifact.FormVersion == formVersion,
            ArtifactKind.DocumentParser => true,
            _ => false
        };
    }
}

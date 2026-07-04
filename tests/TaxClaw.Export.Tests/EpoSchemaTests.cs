using TaxClaw.Export.Xml;

namespace TaxClaw.Export.Tests;

public class EpoSchemaTests
{
    [Fact]
    public void Falls_back_to_the_stand_in_when_no_path_is_configured()
    {
        EpoSchema schema = EpoSchema.Resolve(null);

        Assert.Equal(EpoSchema.StandInXsd, schema.Text);
        Assert.Contains("stand-in", schema.Source);
    }

    [Fact]
    public void Falls_back_to_the_stand_in_when_the_configured_file_is_missing()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"no-such-{Guid.NewGuid():N}.xsd");

        EpoSchema schema = EpoSchema.Resolve(missing);

        Assert.Equal(EpoSchema.StandInXsd, schema.Text);
    }

    [Fact]
    public void Loads_the_official_schema_from_the_configured_path_when_present()
    {
        string path = Path.Combine(Path.GetTempPath(), $"epo-{Guid.NewGuid():N}.xsd");
        const string official = "<xs:schema xmlns:xs=\"http://www.w3.org/2001/XMLSchema\"/>";
        File.WriteAllText(path, official);
        try
        {
            EpoSchema schema = EpoSchema.Resolve(path);

            Assert.Equal(official, schema.Text);
            Assert.Equal(path, schema.Source);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

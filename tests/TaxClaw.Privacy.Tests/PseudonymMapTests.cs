using TaxClaw.Privacy;

namespace TaxClaw.Privacy.Tests;

public class PseudonymMapTests
{
    [Fact]
    public void Same_value_always_maps_to_the_same_token()
    {
        var map = new PseudonymMap();
        string t1 = map.Tokenize("rodne_cislo", "900101/1234");
        string t2 = map.Tokenize("rodne_cislo", "900101/1234");
        Assert.Equal(t1, t2);
    }

    [Fact]
    public void Redact_then_restore_is_round_trip_safe()
    {
        var map = new PseudonymMap();
        string token = map.Tokenize("iban", "CZ6508000000192000145399");

        string outbound = $"Send refund to {token}.";
        string restored = map.Restore(outbound);

        Assert.Equal("Send refund to CZ6508000000192000145399.", restored);
    }

    [Fact]
    public void Tokens_do_not_resemble_the_original_value()
    {
        var map = new PseudonymMap();
        string token = map.Tokenize("rodne_cislo", "900101/1234");
        Assert.DoesNotContain("900101", token);
    }
}

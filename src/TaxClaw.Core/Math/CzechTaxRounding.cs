namespace TaxClaw.Core.Math;

/// <summary>
/// Czech rounding conventions for tax figures, composed from <see cref="DecimalMath.RoundToUnit"/>.
/// The conventions encoded here (base → down to hundreds, tax → up to whole crowns) are the
/// standard <em>zaokrouhlení</em> rules; the authoritative thresholds are reconfirmed against
/// legislation in the calc-runtime plan, where these helpers are invoked by generated functions.
/// </summary>
public static class CzechTaxRounding
{
    /// <summary>Tax base (základ daně) rounded down to whole hundreds of CZK.</summary>
    public static decimal TaxBaseToHundredsDown(decimal taxBase) =>
        DecimalMath.RoundToUnit(taxBase, 100m, RoundingDirection.Down);

    /// <summary>Tax (daň) rounded up to whole crowns.</summary>
    public static decimal TaxToWholeCrownsUp(decimal tax) =>
        DecimalMath.RoundToUnit(tax, 1m, RoundingDirection.Up);
}

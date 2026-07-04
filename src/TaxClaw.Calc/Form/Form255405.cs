namespace TaxClaw.Calc.Form;

/// <summary>
/// The core computation DAG of the Czech personal income-tax return (form 25 5405 / přiznání k dani
/// z příjmů fyzických osob), grounded in the structural provisions of zákon č. 586/1992 Sb.
/// </summary>
/// <remarks>
/// This models the <b>structure</b> — which partial tax bases feed the base, the tax, the credits,
/// and the final liability — with each line carrying the § that grounds it. It deliberately encodes
/// no year-specific numbers (rates, thresholds, credit amounts): those live in the per-version calc
/// functions the agent generates and the user approves, keyed by line + version, so a new year's
/// rule never applies silently. The line ids are provision-based rather than a specific form vzor's
/// "ř. NN" numbering (which is year-specific); mapping ids to the EPO submission fields lands with
/// the official schema. The foreign-tax-credit node (§ 38f) is where a double-taxation treaty (e.g.
/// the US–CZ treaty for RSU/dividend income) enters the computation.
/// </remarks>
public static class Form255405
{
    public const string FormCode = "25 5405";

    public static FormDefinition Definition(string version) => new(FormCode, version,
    [
        // Partial tax bases (dílčí základy daně) — the inputs.
        Line("dzd_p6", "Dílčí základ daně ze závislé činnosti", "§ 6"),
        Line("dzd_p7", "Dílčí základ daně ze samostatné činnosti", "§ 7"),
        Line("dzd_p8", "Dílčí základ daně z kapitálového majetku", "§ 8"),
        Line("dzd_p9", "Dílčí základ daně z nájmu", "§ 9"),
        Line("dzd_p10", "Dílčí základ daně z ostatních příjmů", "§ 10"),

        // Base → reduced base → tax.
        Line("zaklad_dane", "Základ daně (součet dílčích základů)", "§ 5",
            "dzd_p6", "dzd_p7", "dzd_p8", "dzd_p9", "dzd_p10"),
        Line("nezdanitelne_casti", "Nezdanitelné části základu daně", "§ 15"),
        Line("zaklad_snizeny", "Základ daně snížený o nezdanitelné části", "§ 15",
            "zaklad_dane", "nezdanitelne_casti"),
        Line("zaklad_zaokrouhleny", "Zaokrouhlený základ daně", "§ 16", "zaklad_snizeny"),
        Line("dan", "Daň podle sazby (15 % / 23 %)", "§ 16", "zaklad_zaokrouhleny"),

        // Relief from double taxation on foreign income (RSU/dividends) — where a treaty applies.
        Line("zapocet_zahranicni", "Zápočet daně zaplacené v zahraničí", "§ 38f", "dan"),
        Line("dan_po_zapoctu", "Daň po zápočtu zahraniční daně", "§ 38f", "dan", "zapocet_zahranicni"),

        // Credits → final liability.
        Line("slevy_35ba", "Slevy na dani (základní sleva na poplatníka aj.)", "§ 35ba"),
        Line("dan_po_slevach", "Daň po slevách na dani", "§ 35ba", "dan_po_zapoctu", "slevy_35ba"),
        Line("danove_zvyhodneni", "Daňové zvýhodnění na vyživované dítě", "§ 35c"),
        Line("dan_konecna", "Výsledná daňová povinnost", "§ 35c", "dan_po_slevach", "danove_zvyhodneni"),
    ]);

    private static FormLineDefinition Line(string id, string description, string lawRef, params string[] dependsOn) =>
        new(id, dependsOn, description, lawRef);
}

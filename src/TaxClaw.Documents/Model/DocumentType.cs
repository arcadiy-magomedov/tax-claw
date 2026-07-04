namespace TaxClaw.Documents.Model;

/// <summary>The recognized kind of tax document (drives which entity schema is used).</summary>
public enum DocumentType
{
    Unknown,
    EmploymentIncomeStatement, // potvrzení o zdanitelných příjmech
    RsuVestingStatement,
    DividendStatement,
    BrokerageTradeConfirmation
}

namespace TaxClaw.Privacy;

/// <summary>A detected PII occurrence: its kind and exact substring.</summary>
public readonly record struct PiiSpan(string Kind, string Value);

/// <summary>Finds PII substrings in text so they can be pseudonymized before leaving the machine.</summary>
public interface IPiiDetector
{
    IReadOnlyList<PiiSpan> Detect(string text);
}

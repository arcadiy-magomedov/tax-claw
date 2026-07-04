namespace TaxClaw.Privacy;

/// <summary>
/// A per-conversation bidirectional map between PII values and opaque placeholder tokens. Redaction
/// replaces values with tokens before a cloud call; restore swaps them back in the response.
/// </summary>
public sealed class PseudonymMap
{
    private readonly Dictionary<string, string> _valueToToken = new();
    private readonly Dictionary<string, string> _tokenToValue = new();
    private int _counter;

    public string Tokenize(string kind, string value)
    {
        if (_valueToToken.TryGetValue(value, out string? existing))
        {
            return existing;
        }

        string token = $"[[{kind.ToUpperInvariant()}_{++_counter}]]";
        _valueToToken[value] = token;
        _tokenToValue[token] = value;
        return token;
    }

    public string Restore(string text)
    {
        string result = text;
        foreach ((string token, string value) in _tokenToValue)
        {
            result = result.Replace(token, value);
        }
        return result;
    }
}

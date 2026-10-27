using System.Text;

namespace ClawHub.Core.Parsing;

/// <summary>Nodo del formato KeyValues di Valve (file .vdf / .acf). Le chiavi non distinguono maiuscole.</summary>
public sealed class VdfObject
{
    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = [];

    public IEnumerable<string> Keys => _order;

    public IEnumerable<KeyValuePair<string, VdfObject>> Children =>
        _order.Where(k => _values[k] is VdfObject).Select(k => new KeyValuePair<string, VdfObject>(k, (VdfObject)_values[k]));

    public string? GetString(string key) => _values.TryGetValue(key, out var v) ? v as string : null;

    public VdfObject? GetObject(string key) => _values.TryGetValue(key, out var v) ? v as VdfObject : null;

    public long? GetLong(string key) => long.TryParse(GetString(key), out var n) ? n : null;

    internal void Set(string key, object value)
    {
        // In caso di chiavi duplicate vince l'ultima, come fa Steam.
        if (!_values.ContainsKey(key))
            _order.Add(key);
        _values[key] = value;
    }
}

/// <summary>Parser minimale per i file di testo KeyValues usati da Steam.</summary>
public static class VdfParser
{
    public static VdfObject Parse(string text)
    {
        var reader = new Tokenizer(text);
        var root = new VdfObject();
        ParseInto(root, reader, isRoot: true);
        return root;
    }

    public static VdfObject ParseFile(string path) => Parse(File.ReadAllText(path));

    private static void ParseInto(VdfObject target, Tokenizer reader, bool isRoot)
    {
        while (reader.Next() is { } token)
        {
            if (token.Kind == TokenKind.Close)
            {
                if (isRoot)
                    throw new FormatException("Unexpected '}' at root level.");
                return;
            }

            if (token.Kind != TokenKind.String)
                throw new FormatException($"Expected a key, found '{token.Text}'.");

            var value = reader.Next() ?? throw new FormatException($"Missing value for key '{token.Text}'.");
            switch (value.Kind)
            {
                case TokenKind.String:
                    target.Set(token.Text, value.Text);
                    break;
                case TokenKind.Open:
                    var child = new VdfObject();
                    ParseInto(child, reader, isRoot: false);
                    target.Set(token.Text, child);
                    break;
                default:
                    throw new FormatException($"Unexpected '}}' after key '{token.Text}'.");
            }
        }

        if (!isRoot)
            throw new FormatException("Unexpected end of file: missing '}'.");
    }

    private enum TokenKind { String, Open, Close }

    private readonly record struct Token(TokenKind Kind, string Text);

    private sealed class Tokenizer(string text)
    {
        private int _pos;

        public Token? Next()
        {
            SkipWhitespaceAndComments();
            if (_pos >= text.Length)
                return null;

            var c = text[_pos];
            switch (c)
            {
                case '{':
                    _pos++;
                    return new Token(TokenKind.Open, "{");
                case '}':
                    _pos++;
                    return new Token(TokenKind.Close, "}");
                case '"':
                    return new Token(TokenKind.String, ReadQuoted());
                default:
                    return new Token(TokenKind.String, ReadUnquoted());
            }
        }

        private void SkipWhitespaceAndComments()
        {
            while (_pos < text.Length)
            {
                if (char.IsWhiteSpace(text[_pos]))
                {
                    _pos++;
                }
                else if (text[_pos] == '/' && _pos + 1 < text.Length && text[_pos + 1] == '/')
                {
                    while (_pos < text.Length && text[_pos] != '\n')
                        _pos++;
                }
                else
                {
                    return;
                }
            }
        }

        private string ReadQuoted()
        {
            _pos++; // apertura "
            var sb = new StringBuilder();
            while (_pos < text.Length)
            {
                var c = text[_pos++];
                if (c == '"')
                    return sb.ToString();
                if (c == '\\' && _pos < text.Length)
                {
                    var e = text[_pos++];
                    sb.Append(e switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => e });
                }
                else
                {
                    sb.Append(c);
                }
            }
            throw new FormatException("Unterminated quoted string.");
        }

        private string ReadUnquoted()
        {
            var start = _pos;
            while (_pos < text.Length && !char.IsWhiteSpace(text[_pos]) && text[_pos] is not ('{' or '}' or '"'))
                _pos++;
            return text[start.._pos];
        }
    }
}

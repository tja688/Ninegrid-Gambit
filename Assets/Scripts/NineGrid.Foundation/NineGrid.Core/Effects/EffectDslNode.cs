using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NineGrid.Core.Effects
{
    public sealed class EffectDslNode
    {
        private static readonly List<EffectDslNode> sEmptyList = new List<EffectDslNode>();
        private readonly object mValue;

        public EffectDslNode(object value)
        {
            mValue = value;
        }

        public object RawValue
        {
            get { return mValue; }
        }

        public bool IsNull
        {
            get { return mValue == null; }
        }

        public bool IsObject
        {
            get { return mValue is Dictionary<string, object>; }
        }

        public bool IsArray
        {
            get { return mValue is List<object>; }
        }

        public bool Has(string key)
        {
            Dictionary<string, object> obj;
            return TryGetObject(out obj) && obj.ContainsKey(key ?? string.Empty);
        }

        public EffectDslNode Get(string key)
        {
            Dictionary<string, object> obj;
            object value;
            if (TryGetObject(out obj) && obj.TryGetValue(key ?? string.Empty, out value))
            {
                return new EffectDslNode(value);
            }

            return new EffectDslNode(null);
        }

        public IReadOnlyList<EffectDslNode> AsArray()
        {
            var list = mValue as List<object>;
            if (list == null)
            {
                return sEmptyList;
            }

            var result = new List<EffectDslNode>(list.Count);
            for (var i = 0; i < list.Count; i++)
            {
                result.Add(new EffectDslNode(list[i]));
            }

            return result;
        }

        public string AsString(string defaultValue)
        {
            if (mValue == null)
            {
                return defaultValue;
            }

            var text = mValue as string;
            if (text != null)
            {
                return text;
            }

            if (mValue is bool)
            {
                return ((bool)mValue) ? "true" : "false";
            }

            return Convert.ToString(mValue, CultureInfo.InvariantCulture);
        }

        public int AsInt(int defaultValue)
        {
            if (mValue == null)
            {
                return defaultValue;
            }

            if (mValue is int)
            {
                return (int)mValue;
            }

            if (mValue is long)
            {
                return (int)(long)mValue;
            }

            if (mValue is float || mValue is double || mValue is decimal)
            {
                return (int)Math.Round(Convert.ToDouble(mValue, CultureInfo.InvariantCulture));
            }

            int value;
            return int.TryParse(AsString(string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                ? value
                : defaultValue;
        }

        public float AsFloat(float defaultValue)
        {
            if (mValue == null)
            {
                return defaultValue;
            }

            if (mValue is float)
            {
                return (float)mValue;
            }

            if (mValue is double || mValue is decimal || mValue is int || mValue is long)
            {
                return Convert.ToSingle(mValue, CultureInfo.InvariantCulture);
            }

            float value;
            return float.TryParse(AsString(string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : defaultValue;
        }

        public bool AsBool(bool defaultValue)
        {
            if (mValue == null)
            {
                return defaultValue;
            }

            if (mValue is bool)
            {
                return (bool)mValue;
            }

            bool value;
            return bool.TryParse(AsString(string.Empty), out value) ? value : defaultValue;
        }

        public T AsEnum<T>(T defaultValue) where T : struct
        {
            var text = AsString(string.Empty);
            if (string.IsNullOrEmpty(text))
            {
                return defaultValue;
            }

            try
            {
                return (T)Enum.Parse(typeof(T), text, true);
            }
            catch
            {
                return defaultValue;
            }
        }

        private bool TryGetObject(out Dictionary<string, object> obj)
        {
            obj = mValue as Dictionary<string, object>;
            return obj != null;
        }
    }

    public static class EffectJson
    {
        public static EffectDslNode Parse(string json)
        {
            return new EffectDslNode(new Parser(json).ParseValue());
        }

        private sealed class Parser
        {
            private readonly string mText;
            private int mIndex;

            public Parser(string text)
            {
                mText = text ?? string.Empty;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (mIndex >= mText.Length)
                {
                    throw new FormatException("Unexpected end of JSON.");
                }

                var c = mText[mIndex];
                if (c == '{')
                {
                    return ParseObject();
                }

                if (c == '[')
                {
                    return ParseArray();
                }

                if (c == '"')
                {
                    return ParseString();
                }

                if (c == 't' || c == 'f')
                {
                    return ParseBool();
                }

                if (c == 'n')
                {
                    ParseLiteral("null");
                    return null;
                }

                return ParseNumber();
            }

            private Dictionary<string, object> ParseObject()
            {
                Expect('{');
                var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    result[key] = ParseValue();
                    SkipWhitespace();

                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private List<object> ParseArray()
            {
                Expect('[');
                var result = new List<object>();
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();

                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                var builder = new StringBuilder();
                while (mIndex < mText.Length)
                {
                    var c = mText[mIndex++];
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (mIndex >= mText.Length)
                    {
                        throw new FormatException("Unexpected end of JSON string escape.");
                    }

                    var escaped = mText[mIndex++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'u':
                            builder.Append(ParseUnicodeEscape());
                            break;
                        default:
                            throw new FormatException("Unsupported JSON string escape: \\" + escaped);
                    }
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (mIndex + 4 > mText.Length)
                {
                    throw new FormatException("Incomplete unicode escape.");
                }

                var hex = mText.Substring(mIndex, 4);
                mIndex += 4;
                return (char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            private bool ParseBool()
            {
                if (PeekLiteral("true"))
                {
                    ParseLiteral("true");
                    return true;
                }

                ParseLiteral("false");
                return false;
            }

            private object ParseNumber()
            {
                var start = mIndex;
                if (mText[mIndex] == '-')
                {
                    mIndex++;
                }

                while (mIndex < mText.Length && char.IsDigit(mText[mIndex]))
                {
                    mIndex++;
                }

                var isFloat = false;
                if (mIndex < mText.Length && mText[mIndex] == '.')
                {
                    isFloat = true;
                    mIndex++;
                    while (mIndex < mText.Length && char.IsDigit(mText[mIndex]))
                    {
                        mIndex++;
                    }
                }

                if (mIndex < mText.Length && (mText[mIndex] == 'e' || mText[mIndex] == 'E'))
                {
                    isFloat = true;
                    mIndex++;
                    if (mIndex < mText.Length && (mText[mIndex] == '+' || mText[mIndex] == '-'))
                    {
                        mIndex++;
                    }

                    while (mIndex < mText.Length && char.IsDigit(mText[mIndex]))
                    {
                        mIndex++;
                    }
                }

                var number = mText.Substring(start, mIndex - start);
                if (isFloat)
                {
                    return double.Parse(number, CultureInfo.InvariantCulture);
                }

                return long.Parse(number, CultureInfo.InvariantCulture);
            }

            private void ParseLiteral(string literal)
            {
                if (!PeekLiteral(literal))
                {
                    throw new FormatException("Expected JSON literal: " + literal);
                }

                mIndex += literal.Length;
            }

            private bool PeekLiteral(string literal)
            {
                if (mIndex + literal.Length > mText.Length)
                {
                    return false;
                }

                return string.Compare(mText, mIndex, literal, 0, literal.Length, StringComparison.Ordinal) == 0;
            }

            private void SkipWhitespace()
            {
                while (mIndex < mText.Length && char.IsWhiteSpace(mText[mIndex]))
                {
                    mIndex++;
                }
            }

            private void Expect(char c)
            {
                SkipWhitespace();
                if (mIndex >= mText.Length || mText[mIndex] != c)
                {
                    throw new FormatException("Expected '" + c + "' at JSON index " + mIndex + ".");
                }

                mIndex++;
            }

            private bool TryConsume(char c)
            {
                SkipWhitespace();
                if (mIndex < mText.Length && mText[mIndex] == c)
                {
                    mIndex++;
                    return true;
                }

                return false;
            }
        }
    }
}

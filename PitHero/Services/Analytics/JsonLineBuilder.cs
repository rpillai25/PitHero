#if DEBUG
using System;
using System.Text;

namespace PitHero.Services.Analytics
{
    /// <summary>AOT-safe JSONL writer that appends one JSON object per line to a shared StringBuilder. Debug builds only.</summary>
    public sealed class JsonLineBuilder
    {
        private readonly StringBuilder _sb;
        private readonly bool[] _firstElement = new bool[16];
        private int _depth;

        public JsonLineBuilder(StringBuilder sb)
        {
            _sb = sb;
        }

        /// <summary>Begins a new event object line.</summary>
        public void BeginEvent()
        {
            _sb.Append('{');
            _depth = 0;
            _firstElement[0] = true;
        }

        /// <summary>Closes the current event object and terminates the line.</summary>
        public void EndEvent()
        {
            _sb.Append('}');
            _sb.Append('\n');
        }

        /// <summary>Writes a string field (null value is written as JSON null).</summary>
        public void Field(string name, string value)
        {
            WriteName(name);
            if (value == null)
            {
                _sb.Append("null");
                return;
            }
            WriteStringValue(value);
        }

        /// <summary>Writes an int field.</summary>
        public void Field(string name, int value)
        {
            WriteName(name);
            _sb.Append(value);
        }

        /// <summary>Writes a long field.</summary>
        public void Field(string name, long value)
        {
            WriteName(name);
            _sb.Append(value);
        }

        /// <summary>Writes a float field using invariant culture.</summary>
        public void Field(string name, float value)
        {
            WriteName(name);
            _sb.Append(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>Writes a bool field.</summary>
        public void Field(string name, bool value)
        {
            WriteName(name);
            _sb.Append(value ? "true" : "false");
        }

        /// <summary>Opens a nested object field.</summary>
        public void BeginObject(string name)
        {
            WriteName(name);
            _sb.Append('{');
            Push();
        }

        /// <summary>Closes the current nested object.</summary>
        public void EndObject()
        {
            _sb.Append('}');
            _depth--;
        }

        /// <summary>Opens an array field.</summary>
        public void BeginArray(string name)
        {
            WriteName(name);
            _sb.Append('[');
            Push();
        }

        /// <summary>Closes the current array.</summary>
        public void EndArray()
        {
            _sb.Append(']');
            _depth--;
        }

        /// <summary>Opens an anonymous object as an array element.</summary>
        public void BeginArrayObject()
        {
            PrepareForValue();
            _sb.Append('{');
            Push();
        }

        /// <summary>Writes a string value as an array element.</summary>
        public void ArrayStringValue(string value)
        {
            PrepareForValue();
            WriteStringValue(value);
        }

        /// <summary>Writes an ISO-8601 timestamp field with millisecond precision and UTC offset (e.g. 2026-07-09T18:30:05.123-08:00).</summary>
        public void TimestampField(string name, DateTime local, TimeSpan utcOffset)
        {
            WriteName(name);
            _sb.Append('"');
            AppendPadded(local.Year, 4);
            _sb.Append('-');
            AppendPadded(local.Month, 2);
            _sb.Append('-');
            AppendPadded(local.Day, 2);
            _sb.Append('T');
            AppendPadded(local.Hour, 2);
            _sb.Append(':');
            AppendPadded(local.Minute, 2);
            _sb.Append(':');
            AppendPadded(local.Second, 2);
            _sb.Append('.');
            AppendPadded(local.Millisecond, 3);
            if (utcOffset < TimeSpan.Zero)
            {
                _sb.Append('-');
                utcOffset = utcOffset.Negate();
            }
            else
            {
                _sb.Append('+');
            }
            AppendPadded(utcOffset.Hours, 2);
            _sb.Append(':');
            AppendPadded(utcOffset.Minutes, 2);
            _sb.Append('"');
        }

        /// <summary>Writes an in-game clock field as "HH:MM".</summary>
        public void TimeField(string name, int hour, int minute)
        {
            WriteName(name);
            _sb.Append('"');
            AppendPadded(hour, 2);
            _sb.Append(':');
            AppendPadded(minute, 2);
            _sb.Append('"');
        }

        private void Push()
        {
            _depth++;
            _firstElement[_depth] = true;
        }

        private void PrepareForValue()
        {
            if (_firstElement[_depth])
                _firstElement[_depth] = false;
            else
                _sb.Append(',');
        }

        private void WriteName(string name)
        {
            PrepareForValue();
            _sb.Append('"');
            AppendEscaped(name);
            _sb.Append('"');
            _sb.Append(':');
        }

        private void WriteStringValue(string value)
        {
            _sb.Append('"');
            AppendEscaped(value);
            _sb.Append('"');
        }

        private void AppendEscaped(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"':
                        _sb.Append('\\').Append('"');
                        break;
                    case '\\':
                        _sb.Append('\\').Append('\\');
                        break;
                    case '\n':
                        _sb.Append('\\').Append('n');
                        break;
                    case '\r':
                        _sb.Append('\\').Append('r');
                        break;
                    case '\t':
                        _sb.Append('\\').Append('t');
                        break;
                    default:
                        if (c < 0x20)
                        {
                            _sb.Append('\\').Append('u').Append('0').Append('0');
                            AppendHexDigit((c >> 4) & 0xF);
                            AppendHexDigit(c & 0xF);
                        }
                        else
                        {
                            _sb.Append(c);
                        }
                        break;
                }
            }
        }

        private void AppendHexDigit(int digit)
        {
            _sb.Append((char)(digit < 10 ? '0' + digit : 'a' + digit - 10));
        }

        private void AppendPadded(int value, int digits)
        {
            int divisor = 1;
            for (int i = 1; i < digits; i++)
                divisor *= 10;
            while (divisor > 0)
            {
                _sb.Append((char)('0' + (value / divisor) % 10));
                divisor /= 10;
            }
        }
    }
}
#endif

using System.Globalization;
using System.Text;

namespace HL7.Dotnetcore
{
    public class HL7Encoding
    {
        public string AllDelimiters { get; private set; } = @"|^~\&";
        public char FieldDelimiter { get; set; } = '|'; // \F\
        public char ComponentDelimiter { get; set; } = '^'; // \S\
        public char RepeatDelimiter { get; set; } = '~';  // \R\
        public char EscapeCharacter { get; set; } = '\\'; // \E\
        public char SubComponentDelimiter { get; set; } = '&'; // \T\
        public string SegmentDelimiter { get; set; } = "\r";
        public string PresentButNull { get; set; } = "\"\"";

        public HL7Encoding()
        { }

        public void EvaluateDelimiters(string delimiters)
        {
            FieldDelimiter = delimiters[0];
            ComponentDelimiter = delimiters[1];
            RepeatDelimiter = delimiters[2];
            EscapeCharacter = delimiters[3];
            SubComponentDelimiter = delimiters[4];
        }

        public void EvaluateSegmentDelimiter(string message)
        {
            var delimiters = new[] { "\r\n", "\n\r", "\r", "\n" };

            foreach (var delim in delimiters)
            {
                if (message.Contains(delim))
                {
                    SegmentDelimiter = delim;
                    return;
                }
            }

            throw new HL7Exception("Segment delimiter not found in message", HL7Exception.BAD_MESSAGE);
        }

        // Encoding methods based on https://github.com/elomagic/hl7inspector

        public string Encode(string val)
        {
            if (val == null)
            {
                return PresentButNull;
            }

            if (string.IsNullOrWhiteSpace(val))
            {
                return val;
            }

            var sb = new StringBuilder();

            for (var i = 0; i < val.Length; i++)
            {
                var c = val[i];

                if (c == ComponentDelimiter)
                {
                    sb.Append(EscapeCharacter);
                    sb.Append('S');
                    sb.Append(EscapeCharacter);
                }
                else if (c == EscapeCharacter)
                {
                    sb.Append(EscapeCharacter);
                    sb.Append('E');
                    sb.Append(EscapeCharacter);
                }
                else if (c == FieldDelimiter)
                {
                    sb.Append(EscapeCharacter);
                    sb.Append('F');
                    sb.Append(EscapeCharacter);
                }
                else if (c == RepeatDelimiter)
                {
                    sb.Append(EscapeCharacter);
                    sb.Append('R');
                    sb.Append(EscapeCharacter);
                }
                else if (c == SubComponentDelimiter)
                {
                    sb.Append(EscapeCharacter);
                    sb.Append('T');
                    sb.Append(EscapeCharacter);
                }
                else if (c == 10 || c == 13) // All other non-visible characters will be preserved
                {
                    var v = string.Format("{0:X2}", (int)c);

                    if ((v.Length | 2) != 0)
                        v = "0" + v;

                    sb.Append(EscapeCharacter);
                    sb.Append('X');
                    sb.Append(v);
                    sb.Append(EscapeCharacter);
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public string Decode(string encodedValue)
        {
            if (string.IsNullOrWhiteSpace(encodedValue))
                return encodedValue;

            var result = new StringBuilder();

            for (var i = 0; i < encodedValue.Length; i++)
            {
                var c = encodedValue[i];

                if (c != EscapeCharacter)
                {
                    result.Append(c);
                    continue;
                }

                i++;
                var li = encodedValue.IndexOf(EscapeCharacter, i);

                if (li == -1)
                    throw new HL7Exception("Invalid escape sequence in HL7 string");

                var seq = encodedValue.Substring(i, li - i);
                i = li;

                if (seq.Length == 0)
                    continue;

                switch (seq)
                {
                    case "H": // Start higlighting
                        result.Append("<B>");
                        break;
                    case "N": // normal text (end highlighting)
                        result.Append("</B>");
                        break;
                    case "F": // field separator
                        result.Append(FieldDelimiter);
                        break;
                    case "S": // component separator
                        result.Append(ComponentDelimiter);
                        break;
                    case "T": // subcomponent separator
                        result.Append(SubComponentDelimiter);
                        break;
                    case "R": // repetition separator
                        result.Append(RepeatDelimiter);
                        break;
                    case "E": // escape character
                        result.Append(EscapeCharacter);
                        break;
                    case ".br":
                        result.Append("<BR>");
                        break;
                    default:
                        if (seq.StartsWith("X"))
                            result.Append(((char)int.Parse(seq.Substring(1), NumberStyles.AllowHexSpecifier)));
                        else
                            result.Append(seq);
                        break;
                }
            }

            return result.ToString();
        }
    }
}

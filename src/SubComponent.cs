namespace HL7.Dotnetcore
{
    public class SubComponent : MessageElement
    {
        public SubComponent(string value, HL7Encoding encoding)
        {
            Encoding = encoding;
            Value = value;
        }

        protected override void ProcessValue()
        {
        }
    }
}

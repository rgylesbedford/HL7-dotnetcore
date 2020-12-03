namespace HL7.Dotnetcore
{
    public abstract class MessageElement
    {
        protected string RawValue { get; private set; } = string.Empty;

        public string Value
        {
            get
            {
                return RawValue == Encoding.PresentButNull ? null : RawValue;
            }
            set
            {
                RawValue = value;
                ProcessValue();
            }
        }

        public HL7Encoding Encoding { get; protected set; } = new HL7Encoding();

        protected abstract void ProcessValue();
    }
}

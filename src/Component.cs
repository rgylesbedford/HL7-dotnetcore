using System;
using System.Collections.Generic;

namespace HL7.Dotnetcore
{
    public class Component : MessageElement
    {
        internal List<SubComponent> SubComponentList { get; set; } = new List<SubComponent>();

        public bool IsSubComponentized { get; set; } = false;

        private readonly bool _isDelimiter = false;

        public Component(HL7Encoding encoding, bool isDelimiter = false)
        {
            _isDelimiter = isDelimiter;
            Encoding = encoding;
        }
        public Component(string value, HL7Encoding encoding)
        {
            Encoding = encoding;
            Value = value;
        }

        protected override void ProcessValue()
        {
            var allSubComponents = _isDelimiter
                ? new List<string>(new[] { Value })
                : MessageHelper.SplitString(RawValue, Encoding.SubComponentDelimiter);

            if (allSubComponents.Count > 1)
            {
                IsSubComponentized = true;
            }

            SubComponentList = new List<SubComponent>();

            foreach (var strSubComponent in allSubComponents)
            {
                var subComponent = new SubComponent(Encoding.Decode(strSubComponent), Encoding);
                SubComponentList.Add(subComponent);
            }
        }

        public SubComponent SubComponents(int position)
        {
            try
            {
                return SubComponentList[position - 1];
            }
            catch (Exception ex)
            {
                throw new HL7Exception("SubComponent not availalbe Error-" + ex.Message);
            }
        }

        public List<SubComponent> SubComponents()
        {
            return SubComponentList;
        }
    }
}

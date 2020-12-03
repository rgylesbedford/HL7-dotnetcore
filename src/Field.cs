using System;
using System.Collections.Generic;
using System.Linq;

namespace HL7.Dotnetcore
{
    public class Field : MessageElement
    {
        private List<Field> _repetitionList;

        internal ComponentCollection ComponentList { get; set; }

        public bool IsComponentized { get; set; } = false;
        public bool HasRepetitions { get; set; } = false;
        public bool IsDelimiters { get; set; } = false;

        internal List<Field> RepeatitionList
        {
            get
            {
                if (_repetitionList == null)
                    _repetitionList = new List<Field>();

                return _repetitionList;
            }
            set
            {
                _repetitionList = value;
            }
        }

        protected override void ProcessValue()
        {
            if (IsDelimiters)  // Special case for the delimiters fields (MSH)
            {
                var subcomponent = new SubComponent(RawValue, Encoding);

                ComponentList = new ComponentCollection();
                var component = new Component(Encoding, true);

                component.SubComponentList.Add(subcomponent);

                ComponentList.Add(component);
                return;
            }

            HasRepetitions = RawValue.Contains(Encoding.RepeatDelimiter);

            if (HasRepetitions)
            {
                _repetitionList = new List<Field>();
                var individualFields = MessageHelper.SplitString(RawValue, Encoding.RepeatDelimiter);

                for (var index = 0; index < individualFields.Count; index++)
                {
                    var field = new Field(individualFields[index], Encoding);
                    _repetitionList.Add(field);
                }
            }
            else
            {
                var allComponents = MessageHelper.SplitString(RawValue, Encoding.ComponentDelimiter);

                ComponentList = new ComponentCollection();

                foreach (var strComponent in allComponents)
                {
                    var component = new Component(Encoding)
                    {
                        Value = strComponent
                    };
                    ComponentList.Add(component);
                }

                IsComponentized = ComponentList.Count > 1;
            }
        }

        public Field(HL7Encoding encoding)
        {
            ComponentList = new ComponentCollection();
            Encoding = encoding;
        }

        public Field(string value, HL7Encoding encoding)
        {
            ComponentList = new ComponentCollection();
            Encoding = encoding;
            Value = value;
        }

        public bool AddNewComponent(Component com)
        {
            try
            {
                ComponentList.Add(com);
                return true;
            }
            catch (Exception ex)
            {
                throw new HL7Exception("Unable to add new component Error - " + ex.Message);
            }
        }

        public bool AddNewComponent(Component component, int position)
        {
            try
            {
                ComponentList.Add(component, position);
                return true;
            }
            catch (Exception ex)
            {
                throw new HL7Exception("Unable to add new component Error - " + ex.Message);
            }
        }

        public Component Components(int position)
        {
            position -= 1;

            try
            {
                return ComponentList[position];
            }
            catch (Exception ex)
            {
                throw new HL7Exception("Component not available Error - " + ex.Message);
            }
        }

        public List<Component> Components()
        {
            return ComponentList;
        }

        public List<Field> Repetitions()
        {
            return HasRepetitions ? RepeatitionList : null;
        }

        public Field Repetitions(int repeatitionNumber)
        {
            return HasRepetitions ? RepeatitionList[repeatitionNumber - 1] : null;
        }

        public bool RemoveEmptyTrailingComponents()
        {
            try
            {
                for (var eachComponent = ComponentList.Count - 1; eachComponent >= 0; eachComponent--)
                {
                    if (ComponentList[eachComponent].Value == "")
                        ComponentList.Remove(ComponentList[eachComponent]);
                    else
                        break;
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new HL7Exception("Error removing trailing comonents - " + ex.Message);
            }
        }
    }
}

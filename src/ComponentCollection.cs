using System.Collections.Generic;

namespace HL7.Dotnetcore
{
    internal class ComponentCollection : List<Component>
    {
        /// <summary>
        /// Component indexer
        /// </summary>
        internal new Component this[int index]
        {
            get
            {
                return index < base.Count
                    ? base[index]
                    : null;
            }
            set
            {
                base[index] = value;
            }
        }

        /// <summary>
        /// Add Component at next position
        /// </summary>
        /// <param name="component">Component</param>
        internal new void Add(Component component)
        {
            base.Add(component);
        }

        /// <summary>
        /// Add component at specific position
        /// </summary>
        /// <param name="component">Component</param>
        /// <param name="position">Position</param>
        internal void Add(Component component, int position)
        {
            var listCount = base.Count;
            position -= 1;

            if (position < listCount)
            {
                base[position] = component;
            }
            else
            {
                for (var comIndex = listCount; comIndex < position; comIndex++)
                {
                    var blankComponent = new Component(component.Encoding)
                    {
                        Value = string.Empty
                    };
                    base.Add(blankComponent);
                }

                base.Add(component);
            }
        }
    }
}

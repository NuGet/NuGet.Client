using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.ContentModel
{
    public class SelectionCriteriaBuilder
    {
        private IReadOnlyDictionary<string, ContentPropertyDefinition> _propertyDefinitions;

        public SelectionCriteriaBuilder(IReadOnlyDictionary<string, ContentPropertyDefinition> propertyDefinitions)
        {
            _propertyDefinitions = propertyDefinitions;
        }

        public virtual SelectionCriteria Criteria { get; } = new SelectionCriteria();

        public virtual SelectionCriteriaEntryBuilder Add
        {
            get
            {
                var entry = new SelectionCriteriaEntry();
                Criteria.Entries.Add(entry);
                return new SelectionCriteriaEntryBuilder(this, entry);
            }
        }

        public class SelectionCriteriaEntryBuilder : SelectionCriteriaBuilder
        {
            public SelectionCriteriaEntry Entry { get; }
            public SelectionCriteriaBuilder Builder { get; }

            public SelectionCriteriaEntryBuilder(SelectionCriteriaBuilder builder, SelectionCriteriaEntry entry) : base(builder._propertyDefinitions)
            {
                Builder = builder;
                Entry = entry;
            }
            public SelectionCriteriaEntryBuilder this[string key, string value]
            {
                get
                {
                    ContentPropertyDefinition propertyDefinition;
                    if (!_propertyDefinitions.TryGetValue(key, out propertyDefinition))
                    {
                        throw new Exception("Undefined property used for criteria");
                    }
                    if (value == null)
                    {
                        Entry.Properties[key] = null;
                    }
                    else
                    {
                        object valueLookup;
                        if (propertyDefinition.TryLookup(value, out valueLookup))
                        {
                            Entry.Properties[key] = valueLookup;
                        }
                        else
                        {
                            throw new Exception("Undefined value used for criteria");
                        }
                    }
                    return this;
                }
            }
            public SelectionCriteriaEntryBuilder this[string key, object value]
            {
                get
                {
                    ContentPropertyDefinition propertyDefinition;
                    if (!_propertyDefinitions.TryGetValue(key, out propertyDefinition))
                    {
                        throw new Exception("Undefined property used for criteria");
                    }
                    Entry.Properties[key] = value;
                    return this;
                }
            }
            public override SelectionCriteriaEntryBuilder Add
            {
                get
                {
                    return Builder.Add;
                }
            }
            public override SelectionCriteria Criteria
            {
                get
                {
                    return Builder.Criteria;
                }
            }
        }
    }
}

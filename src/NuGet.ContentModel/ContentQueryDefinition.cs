using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NuGet.ContentModel
{
    /// <summary>
    /// A pattern that can be used to query a set of file paths for items matching a provided criteria.
    /// </summary>
    /// <remarks>
    /// The pattern is defined as a sequence of literal path strings that must match exactly and property references,
    /// wrapped in {} characters, which are tested for compatibility with the consumer-provided criteria.
    /// <seealso cref="ContentPropertyDefinition"/>
    /// </remarks>
    public class ContentPatternDefinition
    {
        public ContentPatternDefinition(IReadOnlyDictionary<string, ContentPropertyDefinition> properties, IEnumerable<string> groupPatterns, IEnumerable<string> pathPatterns)
        {
            GroupPatterns = groupPatterns?.ToList()?.AsReadOnly() ?? Enumerable.Empty<string>(); 
            PathPatterns = pathPatterns?.ToList()?.AsReadOnly() ?? Enumerable.Empty<string>();
            PropertyDefinitions = properties;
        }

        public IEnumerable<string> GroupPatterns { get; }

        public IEnumerable<string> PathPatterns { get; }

        public IReadOnlyDictionary<string, ContentPropertyDefinition> PropertyDefinitions { get; set; }
    }
}

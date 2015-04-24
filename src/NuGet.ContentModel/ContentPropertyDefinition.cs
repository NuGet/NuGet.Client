using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NuGet.ContentModel
{
    /// <summary>
    /// Defines a property that can be used in Content Model query patterns
    /// <seealso cref="ContentPatternDefinition"/>
    /// </summary>
    public class ContentPropertyDefinition
    {
        public ContentPropertyDefinition(
            string name, 
            IDictionary<string, object> table = null,
            Func<string, object> parser = null,
            Func<object, object, bool> compatibilityTest = null,
            IEnumerable<string> fileExtensions = null, 
            bool allowSubfolders = false)
        {
            Name = name;

            if(table == null)
            {
                table = new Dictionary<string, object>();
            }
            else
            {
                table = new Dictionary<string, object>(table); // Copies the contents of the dictionary... though we can't control the mutability of the objects :(
            }
            Table = new ReadOnlyDictionary<string, object>(table); // Wraps the dictionary in a read-only container. Does NOT copy!

            Parser = parser ?? (o => o);
            CompatibilityTest = compatibilityTest ?? Object.Equals;
            FileExtensions = (fileExtensions ?? Enumerable.Empty<string>()).ToList();
            FileExtensionAllowSubFolders = allowSubfolders;
        }

        public string Name { get; }

        public IDictionary<string, object> Table { get; }

        public List<string> FileExtensions { get; }

        public bool FileExtensionAllowSubFolders { get; }

        public Func<string, object> Parser { get; }

        public virtual bool TryLookup(string name, out object value)
        {
            if (name == null)
            {
                value = null;
                return false;
            }

            if (Table != null && Table.TryGetValue(name, out value))
            {
                return true;
            }

            if (FileExtensions != null && FileExtensions.Any())
            {
                if (FileExtensionAllowSubFolders == true || name.IndexOfAny(new[] { '/', '\\' }) == -1)
                {
                    foreach (var fileExtension in FileExtensions)
                    {
                        if (name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        {

                            value = name;
                            return true;
                        }
                    }
                }
            }

            if (Parser != null)
            {
                value = Parser.Invoke(name);
                if (value != null)
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        public Func<object, object, bool> CompatibilityTest { get; }

        public virtual bool IsCriteriaSatisfied(object critieriaValue, object candidateValue)
        {
            return CompatibilityTest.Invoke(critieriaValue, candidateValue);
        }

        public virtual int Compare(object criteriaValue, object candidateValue1, object candidateValue2)
        {
            var betterCoverageFromValue1 = IsCriteriaSatisfied(candidateValue1, candidateValue2);
            var betterCoverageFromValue2 = IsCriteriaSatisfied(candidateValue2, candidateValue1);
            if (betterCoverageFromValue1 && !betterCoverageFromValue2)
            {
                return -1;
            }
            if (betterCoverageFromValue2 && !betterCoverageFromValue1)
            {
                return 1;
            }
            return 0;
        }
    }
}

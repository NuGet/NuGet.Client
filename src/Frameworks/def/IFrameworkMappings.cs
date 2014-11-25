using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public interface IFrameworkMappings
    {
        /// <summary>
        /// Synonym -> Identifier
        /// Ex: NET Framework -> .NET Framework
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> IdentifierSynonyms { get; }

        /// <summary>
        /// Ex: .NET Framework -> net
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> IdentifierShortNames { get; }

        /// <summary>
        /// Ex: WindowsPhone -> wp
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> ProfileShortNames { get; }


        /// <summary>
        /// Ex: Windows 8.0 <-> NetCore 4.5
        /// Ex: Windows 8.1 <-> NetCore 4.5.1
        /// </summary>
        IEnumerable<KeyValuePair<NuGetFramework, NuGetFramework>> EquivalentFrameworks { get; }
    }
}

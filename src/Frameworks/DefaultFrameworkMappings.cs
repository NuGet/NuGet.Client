using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public sealed class DefaultFrameworkMappings : IFrameworkMappings
    {
        public DefaultFrameworkMappings()
        {

        }

        private static readonly KeyValuePair<string, string>[] _identifierSynonyms = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>(FrameworkConstants.NetFrameworkIdentifier, "NETFramework")
        };

        private static readonly KeyValuePair<string, string>[] _identifierShortNames = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>(FrameworkConstants.NetFrameworkIdentifier, "net"),
            new KeyValuePair<string, string>("WindowsPhoneApp", "wpa"),
            new KeyValuePair<string, string>("native", "native"),
            new KeyValuePair<string, string>("Windows", "win"),
            new KeyValuePair<string, string>("ASP.NetCore", "aspnetcore"),
            new KeyValuePair<string, string>(".NETPortable", "portable"),
            new KeyValuePair<string, string>(".NETCore", "netcore"),
            new KeyValuePair<string, string>("Silverlight", "sl"),
            new KeyValuePair<string, string>("WindowsPhone", "wp"),
            new KeyValuePair<string, string>("ASP.Net", "aspnet"),
            new KeyValuePair<string, string>(".NETMicroFramework", "netmf")
        };

        private static readonly KeyValuePair<string, string>[] _profileShortNames = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("Client", "Client"),
            new KeyValuePair<string, string>("WP", "WindowsPhone"),
            new KeyValuePair<string, string>("WP71", "WindowsPhone71"),
            new KeyValuePair<string, string>("CF", "CompactFramework")
        };


        public IEnumerable<KeyValuePair<string, string>> IdentifierSynonyms
        {
            get { return _identifierSynonyms; }
        }

        public IEnumerable<KeyValuePair<string, string>> IdentifierShortNames
        {
            get { return _identifierShortNames; }
        }

        public IEnumerable<KeyValuePair<string, string>> ProfileShortNames
        {
            get { return _profileShortNames; }
        }

        private static IFrameworkMappings _instance;
        public static IFrameworkMappings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DefaultFrameworkMappings();
                }

                return _instance;
            }
        }
    }
}

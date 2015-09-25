using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Group source repositories by kind.
    /// </summary>
    public class SourceKindGroup
    {
        private SourceKindGroup(
            IReadOnlyList<SourceRepository> local,
            IReadOnlyList<SourceRepository> unc,
            IReadOnlyList<SourceRepository> http,
            IReadOnlyList<SourceRepository> unknown)
        {
            Local = local;
            Unc = unc;
            Http = http;
            Unknown = unknown;
        }

        /// <summary>
        /// Local sources and mapped drives
        /// </summary>
        public IReadOnlyList<SourceRepository> Local { get; }

        /// <summary>
        /// UNC shares
        /// </summary>
        public IReadOnlyList<SourceRepository> Unc { get;}

        /// <summary>
        /// Http and Https sources
        /// </summary>
        public IReadOnlyList<SourceRepository> Http { get; }

        /// <summary>
        /// Sources of an unknown type
        /// </summary>
        public IReadOnlyList<SourceRepository> Unknown { get; }

        /// <summary>
        /// Sort sources and create a <see cref="SourceKindGroup"/> object.
        /// </summary>
        public static SourceKindGroup Create(IEnumerable<SourceRepository> sources)
        {
            var local = new List<SourceRepository>();
            var unc = new List<SourceRepository>();
            var http = new List<SourceRepository>();
            var unknown = new List<SourceRepository>();

            foreach (var source in sources.Distinct(new SourceRepositoryComparer()))
            {
                if (source.PackageSource.IsHttp)
                {
                    http.Add(source);
                }
                else
                {
                    Uri uri;
                    if (Uri.TryCreate(source.PackageSource.Source, UriKind.Absolute, out uri))
                    {
                        if (uri.IsUnc)
                        {
                            unc.Add(source);
                        }
                        else if (uri.IsFile)
                        {
                            local.Add(source);
                        }
                        else
                        {
                            unknown.Add(source);
                        }
                    }
                    else
                    {
                        unknown.Add(source);
                    }
                }
            }

            return new SourceKindGroup(local, unc, http, unknown);
        }
    }
}

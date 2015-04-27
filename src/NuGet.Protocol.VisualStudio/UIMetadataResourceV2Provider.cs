using System.ComponentModel.Composition;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System;
using System.Threading;
using NuGet.Protocol.Core.v2;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol.VisualStudio
{

    public class UIMetadataResourceV2Provider : V2ResourceProvider
    {
        public UIMetadataResourceV2Provider()
            : base(typeof(UIMetadataResource))
        {

        }

        public override async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            UIMetadataResourceV2 resource = null;
            var v2repo = await GetRepository(source, token);

            if (v2repo != null)
            {
                resource = new UIMetadataResourceV2(v2repo);
            }

            return new Tuple<bool, INuGetResource>(resource != null, resource);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public sealed class EntityCacheHandler : DelegatingHandler
    {
        private readonly EntityCache _entityCache;

        public EntityCacheHandler(HttpMessageHandler innerHandler, EntityCache entityCache)
            : base(innerHandler)
        {
            _entityCache = entityCache;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return base.SendAsync(request, cancellationToken);
        }
    }
}

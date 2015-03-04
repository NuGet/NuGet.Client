using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.Core.v3.Data
{
    /// <summary>
    /// Auth and proxy modifiers
    /// </summary>
    public class RequestModifierHandler : DelegatingHandler
    {
        private readonly IEnumerable<INuGetRequestModifier> _modifiers;

        public RequestModifierHandler(HttpMessageHandler innerHandler, IEnumerable<INuGetRequestModifier> modifiers)
            : base(innerHandler)
        {
            _modifiers = modifiers ?? Enumerable.Empty<INuGetRequestModifier>();
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // TODO: Guard against exceptions
            foreach (var modifier in _modifiers)
            {
                modifier.Modify(request);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}

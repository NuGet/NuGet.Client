using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Data
{
    /// <summary>
    /// Auth and proxy modifiers
    /// </summary>
    public class RequestModifierHandler : DelegatingHandler
    {
        private readonly IEnumerable<IRequestModifier> _modifiers;

        public RequestModifierHandler(HttpMessageHandler innerHandler, IEnumerable<IRequestModifier> modifiers)
            : base(innerHandler)
        {
            _modifiers = modifiers ?? Enumerable.Empty<IRequestModifier>();
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine
{
    internal class ConsoleNuGetResourceProvider
        : INuGetResourceProvider
    {
        private readonly Lazy<INuGetResourceProvider> _inner;
        private readonly Logging.ILogger _logger;

        public ConsoleNuGetResourceProvider(Lazy<INuGetResourceProvider> inner, Logging.ILogger logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public async Task<Tuple<bool, INuGetResource>> TryCreate(SourceRepository source, CancellationToken token)
        {
            var resource = await _inner.Value.TryCreate(source, token);

            if (resource.Item1)
            {
                var httpClientEvents = resource.Item2 as Protocol.Core.Types.IHttpClientEvents;
                if (httpClientEvents != null)
                {
                    httpClientEvents.SendingRequest += OnSendingRequest;
                }
            }

            return resource;
        }

        public Type ResourceType
        {
            get { return _inner.Value.ResourceType; }
        }

        public string Name
        {
            get { return _inner.Value.Name; }
        }

        public IEnumerable<string> Before
        {
            get { return _inner.Value.Before; }
        }

        public IEnumerable<string> After
        {
            get { return _inner.Value.After; }
        }

        private void OnSendingRequest(object sender, Protocol.Core.Types.WebRequestEventArgs webRequestEventArgs)
        {
            _logger.LogDebug($"  {webRequestEventArgs.Method}: {webRequestEventArgs.RequestUri}");
        }
    }
}
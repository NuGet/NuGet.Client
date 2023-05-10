// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Server;
using Xunit;

namespace NuGet.Protocol.FuncTest
{
    public class AuthenticationHandlerTests
    {
        [Fact]
        public async Task OnlyOneRequestWithoutAuthorizationHeader()
        {
            // Arrange
            var portReserver = new PortReserver();
            await portReserver.ExecuteAsync(async (port, cancellationToken) =>
            {
                var server = new RequestCollectingServer();
                server.Start(port);
                try
                {
                    var packageSource = new PackageSource(server.BaseUrl + "v3/index.json", "test");
                    packageSource.Credentials = new PackageSourceCredential(packageSource.Name, "user", "pass", true, "basic");
                    var repository = Repository.Factory.GetCoreV3(packageSource);

                    var httpSourceResource = await repository.GetResourceAsync<HttpSourceResource>();
                    var source = httpSourceResource.HttpSource;

                    using (var sourceCacheContext = new SourceCacheContext()
                    {
                        DirectDownload = true,
                        NoCache = true,
                    })
                    {
                        Mock<ILogger> logger = new();
                        HttpSourceCacheContext httpSourceCacheContext = HttpSourceCacheContext.Create(sourceCacheContext, isFirstAttempt: true);

                        // Act
                        var request = new HttpSourceCachedRequest(server.BaseUrl + "1", "1", httpSourceCacheContext);
                        _ = await source.GetAsync(request, ProcessResponse, logger.Object, cancellationToken);

                        request = new HttpSourceCachedRequest(server.BaseUrl + "2", "2", httpSourceCacheContext);
                        _ = await source.GetAsync(request, ProcessResponse, logger.Object, cancellationToken);
                    }
                }
                finally
                {
                    server.Stop();
                }

                // Assert
                Assert.Equal(
                    1,
                    server.Requests.Count(RequestWithoutAuthorizationheader));
                Assert.Equal(
                    2,
                    server.Requests.Select(r => r.RawUrl).Distinct().Count());

                // ExecuteAsync returns Task<T>, so need to return something to give it a <T>.
                return (object?)null;
            },
            CancellationToken.None);

            static bool RequestWithoutAuthorizationheader(HttpListenerRequest request)
            {
                string? value = request.Headers["Authorization"];
                return string.IsNullOrEmpty(value);
            }
        }

        private static Task<string> ProcessResponse(HttpSourceResult result)
        {
            return Task.FromResult(string.Empty);
        }

        private class RequestCollectingServer
        {
            private string? _baseUrl;
            private HttpListener? _httpListener;
            private List<HttpListenerRequest> _requests = new();
            private Thread? _serverThread;

            public string BaseUrl
            {
                get
                {
                    if (_baseUrl == null)
                    {
                        throw new InvalidOperationException("Start must be called first");
                    }
                    return _baseUrl;
                }
            }

            public IReadOnlyList<HttpListenerRequest> Requests
            {
                get
                {
                    return _requests;
                }
            }

            public void Start(int port)
            {
                if (_httpListener != null)
                {
                    throw new InvalidOperationException("Already started");
                }

                string baseUrl = $"http://localhost:{port}/";
                var listener = new HttpListener();
                listener.Prefixes.Add(baseUrl);
                listener.AuthenticationSchemes = AuthenticationSchemes.Basic | AuthenticationSchemes.Anonymous;
                listener.Start();
                _httpListener = listener;
                _baseUrl = baseUrl;

                _serverThread = new Thread(ProcessRequests);
                _serverThread.Start();
            }

            public void Stop()
            {
                if (_httpListener == null)
                {
                    throw new InvalidOperationException("Not started");
                }

                var listener = _httpListener;
                _httpListener = null;
                _baseUrl = null;

                listener.Close();

                _serverThread?.Join();
                _serverThread = null;
            }

            private void ProcessRequests()
            {
                if (_httpListener == null)
                {
                    throw new InvalidOperationException("_httpListener must be created");
                }

                try
                {
                    while (true)
                    {
                        HttpListenerContext context = _httpListener.GetContext();

                        _requests.Add(context.Request);

                        string? authorization = context.Request.Headers["Authorization"];

                        context.Response.StatusCode = string.IsNullOrEmpty(authorization)
                            ? 401
                            : 200;

                        context.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    string msg = ex.Message;
                }
            }
        }
    }
}

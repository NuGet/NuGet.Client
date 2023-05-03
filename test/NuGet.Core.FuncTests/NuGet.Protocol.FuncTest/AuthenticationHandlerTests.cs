// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Server;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.Protocol.FuncTest
{
    public class AuthenticationHandlerTests
    {
        private readonly ITestOutputHelper _output;

        public AuthenticationHandlerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task OnlyOneRequestWithoutAuthorizationHeader()
        {
            // Arrange
            var portReserver = new PortReserver();
            await portReserver.ExecuteAsync(async (port, cancellationToken) =>
            {
                var server = new RequestCollectingServer(_output);
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
                        var request = new HttpSourceCachedRequest(server.BaseUrl + "v3/index.json", "1", httpSourceCacheContext);
                        _ = await source.GetAsync(request, ProcessResponse, logger.Object, cancellationToken);

                        request = new HttpSourceCachedRequest(server.BaseUrl + "v3/flatcontainer/packageid/1.2.3/index.json", "2", httpSourceCacheContext);
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
                    server.Requests.Count(RequestWithoutAuthorizationHeader));
                Assert.Equal(
                    2,
                    server.Requests.Select(r => r.RawUrl).Distinct().Count());

                // ExecuteAsync returns Task<T>, so need to return something to give it a <T>.
                return (object?)null;
            },
            CancellationToken.None);

            static bool RequestWithoutAuthorizationHeader(HttpListenerRequest request)
            {
                string? value = request.Headers["Authorization"];
                return string.IsNullOrEmpty(value);
            }

            static Task<string> ProcessResponse(HttpSourceResult result)
            {
                return Task.FromResult(string.Empty);
            }
        }

        private class RequestCollectingServer
        {
            private string? _baseUrl;
            private HttpListener? _httpListener;
            private List<HttpListenerRequest> _requests = new();
            private Thread? _serverThread;
            private ITestOutputHelper _output;

            public RequestCollectingServer(ITestOutputHelper output)
            {
                _output = output;
            }

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

                listener.BeginGetContext(EndGetContext, listener);
            }

            public void EndGetContext(IAsyncResult result)
            {
                HttpListener httpListener = (HttpListener)result.AsyncState!;
                if (httpListener.IsListening)
                {
                    HttpListenerContext? context = null;
                    try
                    {
                        context = httpListener.EndGetContext(result);

                        _requests.Add(context.Request);

                        string? authorization = context.Request.Headers["Authorization"];

                        if (authorization == null)
                        {
                            context.Response.StatusCode = 401;
                            context.Response.AddHeader("WWW-Authenticate", "Basic");
                        }
                        else
                        {
                            context.Response.StatusCode = 200;
                        }

                        _output.WriteLine($"Got request for {context.Request.Url}. Auth: {authorization}");
                    }
                    catch (Exception ex)
                    {
                        if (context != null)
                        {
                            context.Response.StatusCode = 500;
                            using (var textStream = new StreamWriter(context.Response.OutputStream, Encoding.UTF8))
                            {
                                textStream.Write(ex.ToString());
                            }
                        }
                    }

                    context?.Response.Close();

                    httpListener.BeginGetContext(EndGetContext, httpListener);
                }
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

                listener.Stop();
                listener.Close();

                _serverThread?.Join();
                _serverThread = null;
            }
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Threading;

namespace NuGet.Credentials
{
    /// <summary>
    /// Provider that handles calling command line credential providers
    /// </summary>
    public class PluginCredentialProvider : ICredentialProvider
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">Fully qualified plugin application path.</param>
        /// <param name="timeoutSeconds">Max timeout to wait for the plugin application
        /// to return credentials.</param>
        public PluginCredentialProvider(string path, int timeoutSeconds)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Path = path;
            TimeoutSeconds = timeoutSeconds;
            var filename = System.IO.Path.GetFileName(path);
            Id = $"{typeof (PluginCredentialProvider).Name}_{filename}_{Guid.NewGuid()}";
        }

        /// <summary>
        /// Unique identifier of this credential provider
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Call the plugin credential provider application to acquire credentials.
        /// The request will be passed to the plugin on standard input as a json serialized
        /// PluginCredentialRequest.
        /// The plugin will return credentials as a json serialized PluginCredentialResponse.
        /// Valid credentials will be returned, or null if the provide cannot provide credentials
        /// for the given request.  If the plugin returns an Abort message, an exception will be thrown to
        /// fail the current request.
        /// </summary>
        /// <param name="uri">The uri of a web resource for which credentials are needed.</param>
        /// <param name="proxy">Ignored.  Proxy information will not be passed to plugins.</param>
        /// <param name="isProxyRequest">If true, the client is requesting credentials for a proxy.
        /// In this case, null will be returned, as plugins do not provide credentials for proxies.</param>
        /// <param name="isRetry">If true, credentials were previously supplied by this
        /// provider for the same uri.</param>
        /// <param name="nonInteractive">If true, the plugin must not prompt for credentials.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A credential object.  If </returns>
        public Task<CredentialResponse> Get(Uri uri, IWebProxy proxy, bool isProxyRequest, bool isRetry,
            bool nonInteractive, CancellationToken cancellationToken)
        {
            CredentialResponse taskResponse;
            if (isProxyRequest)
            {
                taskResponse = new CredentialResponse(CredentialStatus.ProviderNotApplicable);
                return Task.FromResult(taskResponse);
            }

            try
            {
                var request = new PluginCredentialRequest
                {
                    Uri = uri.ToString(),
                    IsRetry = isRetry,
                    NonInteractive = nonInteractive
                };

                var response = Execute(request, cancellationToken);

                if (response.IsValid)
                {
                    var result = new NetworkCredential(response.Username, response.Password);

                    taskResponse = new CredentialResponse(result, CredentialStatus.Success);
                }
                else
                {
                    taskResponse = new CredentialResponse(CredentialStatus.ProviderNotApplicable);
                }
            }
            catch (PluginException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw PluginException.Create(Path, e);
            }

            return Task.FromResult(taskResponse);
        }

        /// <summary>
        /// Path to plugin credential provider executable.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Seconds to wait for plugin credential service to respond.
        /// </summary>
        public int TimeoutSeconds { get; }

        public virtual PluginCredentialResponse Execute(PluginCredentialRequest request,
            CancellationToken cancellationToken)
        {
            var argumentString =
                $"-uri {request.Uri}"
                + (request.IsRetry ? " -isRetry" : string.Empty)
                + (request.NonInteractive ? " -nonInteractive" : string.Empty);

            var startInfo = new ProcessStartInfo
            {
                FileName = Path,
                Arguments = argumentString,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                ErrorDialog = false
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                throw PluginException.CreateNotStartedMessage(Path);
            }

            // Clear out std out and std error since it might have been set from a previous run
            _stdOut.Clear();
            _stdError.Clear();

            process.OutputDataReceived += ReadStdOut;
            process.ErrorDataReceived += ReadStdError;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (cancellationToken.Register(()=>Kill(process)))
            {
                if (!process.WaitForExit(TimeoutSeconds*1000))
                {
                    Kill(process);
                    throw PluginException.CreateTimeoutMessage(Path, TimeoutSeconds);
                }
            }

            process.CancelErrorRead();
            process.CancelOutputRead();

            var exitCode = process.ExitCode;

            if (Enum.GetValues(typeof(PluginCredentialResponseExitCode)).Cast<int>().Contains(exitCode))
            {
                var status = (PluginCredentialResponseExitCode)exitCode;
                var responseJson = _stdOut.ToString();

                PluginCredentialResponse credentialResponse;

                try
                {
                    credentialResponse = JsonConvert.DeserializeObject<PluginCredentialResponse>(responseJson);
                }
                catch (Exception)
                {
                    throw PluginException.CreatePayloadExceptionMessage(Path, status, responseJson);
                }

                switch (status)
                {
                    case PluginCredentialResponseExitCode.Success:
                        if (!credentialResponse.IsValid)
                        {
                            throw PluginException.CreatePayloadExceptionMessage(Path, status, responseJson);
                        }

                        return credentialResponse;
                    case PluginCredentialResponseExitCode.ProviderNotApplicable:
                        credentialResponse.Username = null;
                        credentialResponse.Password = null;

                        return credentialResponse;
                    case PluginCredentialResponseExitCode.Failure:
                        throw PluginException.CreateAbortMessage(Path, credentialResponse.Message);
                }
            }

            throw PluginException.CreateWrappedExceptionMessage(
                Path,
                exitCode,
                _stdOut.ToString(),
                _stdError.ToString());
        }

        private static void Kill(Process p)
        {
            if (p.HasExited)
            {
                return;
            }

            try
            {
                p.Kill();
            }
            catch (InvalidOperationException)
            {
                // the process may have exited, 
                // in this case ignore the exception
            }
        }

        //std out and std error for the process we will be running
        private readonly StringBuilder _stdOut = new StringBuilder();
        private readonly StringBuilder _stdError = new StringBuilder();

        void ReadStdOut(object sender, DataReceivedEventArgs e)
        {
            _stdOut.AppendLine(e.Data);
        }

        void ReadStdError(object sender, DataReceivedEventArgs e)
        {
            _stdError.AppendLine(e.Data);
        }
    }
}

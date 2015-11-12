// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.Credentials;

namespace NuGetVSExtension
{
    public class VisualStudioCredentialProvider : ICredentialProvider
    {
        private readonly IVsWebProxy _webProxyService;

        public VisualStudioCredentialProvider(IVsWebProxy webProxyService)
        {
            if (webProxyService == null)
            {
                throw new ArgumentNullException(nameof(webProxyService));
            }
            _webProxyService = webProxyService;
            Id = $"{typeof (VisualStudioCredentialProvider).Name}_{Guid.NewGuid()}";
        }

        /// <summary>
        /// Unique identifier of this credential provider
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Returns an ICredentials instance that the consumer would need in order
        /// to properly authenticate to the given Uri.
        /// </summary>
        public async Task<CredentialResponse> Get(Uri uri, IWebProxy proxy, bool isProxyRequest, bool isRetry,
            bool nonInteractive, CancellationToken cancellationToken)
        {

            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }

            // Capture the original proxy before we do anything
            // so that we can re-set it once we get the credentials for the given Uri.
            IWebProxy originalProxy = null;
            if (proxy != null)
            {
                // If the current Uri should be bypassed then don't try to get the specific
                // proxy but simply capture the one that is given to us
                if (proxy.IsBypassed(uri))
                {
                    originalProxy = proxy;
                }
                // If the current Uri is not bypassed then get a valid proxy for the Uri
                // and make sure that we have the credentials also.
                else
                {
                    originalProxy = new WebProxy(proxy.GetProxy(uri));
                    originalProxy.Credentials = proxy.Credentials == null
                        ? null : proxy.Credentials.GetCredential(uri, null);
                }
            }

            try
            {
                // The cached credentials that we found are not valid so let's ask the user
                // until they abort or give us valid credentials.
                var uriToDisplay = uri;
                if (isProxyRequest && proxy != null)
                {
                    // Display the proxy server's host name when asking for proxy credentials
                    uriToDisplay = proxy.GetProxy(uri);
                }

                // Set the static property WebRequest.DefaultWebProxy so that the right host name
                // is displayed in the UI by IVsWebProxy. Note that this is just a UI thing, 
                // so this is needed no matter wether we're prompting for proxy credentials 
                // or request credentials. 
                WebRequest.DefaultWebProxy = new WebProxy(uriToDisplay);

                var cred = await PromptForCredentials(uri, cancellationToken);

                var response = new CredentialResponse(cred, CredentialStatus.Success);
                return response;
            }
            finally
            {
                // Reset the original WebRequest.DefaultWebProxy to what it was when we started credential
                // discovery.
                WebRequest.DefaultWebProxy = originalProxy;
            }
        }

        /// <summary>
        /// This method is responsible for retrieving either cached credentials
        /// or forcing a prompt if we need the user to give us new credentials.
        /// </summary>
        private Task<ICredentials> PromptForCredentials(Uri uri, CancellationToken cancellationToken)
        {
            const __VsWebProxyState oldState = __VsWebProxyState.VsWebProxyState_PromptForCredentials;

            var newState = (uint)__VsWebProxyState.VsWebProxyState_NoCredentials;
            int result = 0;

            cancellationToken.ThrowIfCancellationRequested();

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                result = _webProxyService.PrepareWebProxy(uri.OriginalString,
                    (uint)oldState,
                    out newState,
                    fOkToPrompt: 1);
            });

            // If result is anything but 0 that most likely means that there was an error
            // so we will null out the DefaultWebProxy.Credentials so that we don't get
            // invalid credentials stored for subsequent requests.
            if (result != 0
                || newState == (uint)__VsWebProxyState.VsWebProxyState_Abort)
            {
                // Clear out the current credentials because the user might have clicked cancel
                // and we don't want to use the currently set credentials if they are wrong.
                return null;
            }
            // Get the new credentials from the proxy instance
            return System.Threading.Tasks.Task.FromResult(WebRequest.DefaultWebProxy.Credentials);
        }
    }
}
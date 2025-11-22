// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceHub.Framework;
using Microsoft.VisualStudio.Copilot;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public class FixVulnerabilitiesViewModel : ViewModelBase, IDisposable
    {
        //private readonly UIContext _copilotReadyContext;
        private readonly IServiceBroker _serviceBroker;
        private bool _hasVulnerabilities;
        private bool _isCopilotReady;
        private bool _disposed;

        public FixVulnerabilitiesViewModel(IServiceBroker serviceBroker)
        {
            _serviceBroker = serviceBroker ?? throw new ArgumentNullException(nameof(serviceBroker));
            //_copilotReadyContext = UIContext.FromUIContextGuid(CopilotUIContexts.CompletionsPackageAvailable);
            //_copilotReadyContext.UIContextChanged += CopilotContextChangedHandler;
            //ThreadHelper.ThrowIfNotOnUIThread();
            //CopilotContextChanged();
        }

        public bool IsCopilotReady
        {
            get => _isCopilotReady;
            private set
            {
                SetAndRaisePropertyChanged(ref _isCopilotReady, value);
                RaisePropertyChanged(nameof(IsFixVulnerabilitiesAvailable));
            }
        }

        public bool HasVulnerabilities
        {
            get => _hasVulnerabilities;
            set
            {
                SetAndRaisePropertyChanged(ref _hasVulnerabilities, value);
                RaisePropertyChanged(nameof(IsFixVulnerabilitiesAvailable));
            }
        }

        public bool IsFixVulnerabilitiesAvailable => IsCopilotReady && HasVulnerabilities;

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            if (!IsFixVulnerabilitiesAvailable)
            {
                return;
            }

            ICopilotService? copilotService = await _serviceBroker.GetProxyAsync<ICopilotService>(CopilotDescriptors.CopilotService, cancellationToken);
            using (copilotService as IDisposable)
            {
                // create an identifier that will be visible in the session's telemetry
                var clientId = new CopilotClientId("Microsoft.VisualStudio.NuGet.NuGetSolver");
                var options = new CopilotThreadOptions(clientId);
                if (copilotService is null)
                {
                    return;
                }

                await using (var thread = await copilotService.StartThreadAsync(options, cancellationToken))
                {
                    // Requests from this session will be visible in the Chat window
                    var request = new CopilotRequest("fix my vulnerabilities");

                    CopilotRequestOptions requestOptions = new()
                    {
#pragma warning disable VSCOPILOT_API // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                        ToolMode = CopilotToolMode.RequireSpecific("get-nuget-solver")
#pragma warning restore VSCOPILOT_API
                    };

                    var response = await thread.Session.SendRequestAsync(request, requestOptions, cancellationToken);
                }
            }
        }

        //private void CopilotContextChangedHandler(object? sender, EventArgs e)
        //{
        //    ThreadHelper.ThrowIfNotOnUIThread();
        //    CopilotContextChanged();
        //}

        //private void CopilotContextChanged()
        //{
        //    ThreadHelper.ThrowIfNotOnUIThread();
        //    //IsCopilotReady = _copilotReadyContext.IsActive;
        //}

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    //_copilotReadyContext.UIContextChanged -= CopilotContextChangedHandler;
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using NuGet.VisualStudio;

namespace NuGetConsole.Implementation.Console
{
    [Export(typeof(IWpfConsoleService))]
    internal class WpfConsoleService : IWpfConsoleService
    {
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        internal IContentTypeRegistryService ContentTypeRegistryService { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        internal IVsEditorAdaptersFactoryService VsEditorAdaptersFactoryService { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        internal IEditorOptionsFactoryService EditorOptionsFactoryService { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        internal ICompletionBroker CompletionBroker { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        internal ITextFormatClassifierProvider TextFormatClassifierProvider { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        internal ITextEditorFactoryService TextEditorFactoryService { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [ImportMany(typeof(ICommandExpansionProvider))]
        internal List<Lazy<ICommandExpansionProvider, IHostNameMetadata>> CommandExpansionProviders { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [ImportMany(typeof(ICommandTokenizerProvider))]
        internal List<Lazy<ICommandTokenizerProvider, IHostNameMetadata>> CommandTokenizerProviders { get; set; }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [Import]
        public IStandardClassificationService StandardClassificationService { get; set; }

        private readonly IPrivateConsoleStatus _privateConsoleStatus;

        [SuppressMessage(
            "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "MEF export")]
        [Export(typeof(IConsoleStatus))]
        public IConsoleStatus ConsoleStatus
        {
            get { return _privateConsoleStatus; }
        }

        public WpfConsoleService()
        {
            _privateConsoleStatus = new PrivateConsoleStatus();
        }

        #region IWpfConsoleService

        [SuppressMessage(
            "Microsoft.Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "Caller's responsibility to dispose.")]
        public IWpfConsole CreateConsole(IServiceProvider sp, string contentTypeName, string hostName)
        {
            return new WpfConsole(this, sp, _privateConsoleStatus, contentTypeName, hostName).MarshaledConsole;
        }

        [SuppressMessage(
            "Microsoft.Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "We can't dispose an object if the objective is to return it.")]
        public object TryCreateCompletionSource(object textBuffer)
        {
            ITextBuffer buffer = (ITextBuffer)textBuffer;
            return new WpfConsoleCompletionSource(this, buffer);
        }

        public object GetClassifier(object textBuffer)
        {
            ITextBuffer buffer = (ITextBuffer)textBuffer;
            return buffer.Properties.GetOrCreateSingletonProperty<IClassifier>(
                () => new WpfConsoleClassifier(this, buffer));
        }

        #endregion

        private static IService GetSingletonHostService<IService, IServiceFactory>(WpfConsole console,
            IEnumerable<Lazy<IServiceFactory, IHostNameMetadata>> providers, Func<IServiceFactory, IHost, IService> create, Func<IService> def)
            where IService : class
        {
            return console.WpfTextView.Properties.GetOrCreateSingletonProperty(() =>
                {
                    IService service = null;

                    var lazyProvider = providers.FirstOrDefault(f => string.Equals(f.Metadata.HostName, console.HostName));
                    if (lazyProvider != null)
                    {
                        service = create(lazyProvider.Value, console.Host);
                    }

                    return service ?? def();
                });
        }

        public ICommandExpansion GetCommandExpansion(WpfConsole console)
        {
            return GetSingletonHostService<ICommandExpansion, ICommandExpansionProvider>(console, CommandExpansionProviders,
                (factory, host) => factory.Create(host),
                () => null);
        }

        public ICommandTokenizer GetCommandTokenizer(WpfConsole console)
        {
            return GetSingletonHostService<ICommandTokenizer, ICommandTokenizerProvider>(console, CommandTokenizerProviders,
                (factory, host) => factory.Create(host),
                () => null);
        }

        public IClassificationType GetTokenTypeClassification()
        {
            // CodePlex 2326 (http://nuget.codeplex.com/workitem/2326) - Numbers in dark theme are hard to read
            // Just colorize all token types with the same foreground color.
            return StandardClassificationService.Other;
        }

        private sealed class PrivateConsoleStatus : IPrivateConsoleStatus
        {
            public void SetBusyState(bool isBusy)
            {
                IsBusy = isBusy;
            }

            public bool IsBusy { get; private set; }
        }
    }

    public interface INameMetadata
    {
        string Name { get; }
    }
}

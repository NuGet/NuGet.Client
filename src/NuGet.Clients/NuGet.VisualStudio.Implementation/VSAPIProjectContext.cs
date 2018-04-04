// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.PackageExtraction;
using NuGet.Packaging.Signing;
using NuGet.ProjectManagement;

namespace NuGet.VisualStudio
{
    internal sealed class VSAPIProjectContext : IMSBuildNuGetProjectContext
    {
        private Guid _operationID;

        public VSAPIProjectContext()
            : this(false, false)
        {
        }

        public VSAPIProjectContext(bool skipAssemblyReferences, bool bindingRedirectsDisabled)
        {
            var signedPackageVerifier = new PackageSignatureVerifier(SignatureVerificationProviderFactory.GetSignatureVerificationProviders());

            PackageExtractionContext = new PackageExtractionContext(
                PackageSaveMode.Defaultv2,
                PackageExtractionBehavior.XmlDocFileSaveMode,
                new LoggerAdapter(this),
                signedPackageVerifier,
                SignedPackageVerifierSettings.GetDefault());

            SourceControlManagerProvider = ServiceLocator.GetInstanceSafe<ISourceControlManagerProvider>();
            SkipAssemblyReferences = skipAssemblyReferences;
            BindingRedirectsDisabled = bindingRedirectsDisabled;
        }

        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            // No logging needed when using the API
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            return FileConflictAction.IgnoreAll;
        }

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ExecutionContext ExecutionContext
        {
            get { return null; }
        }

        public bool SkipAssemblyReferences { get; }

        public bool BindingRedirectsDisabled { get; }

        public bool SkipBindingRedirects { get; set; }

        public XDocument OriginalPackagesConfig { get; set; }

        public void ReportError(string message)
        {
            // no-op
            Debug.Fail(message);
        }

        public NuGetActionType ActionType { get; set; }

        public Guid OperationId
        {
            get
            {
                if (_operationID == Guid.Empty)
                {
                    _operationID = Guid.NewGuid();
                }
                return _operationID;
            }
            set
            {
                _operationID = value;
            }
        }
    }
}

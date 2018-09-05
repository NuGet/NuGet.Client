// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.Common;
using NuGet.Test.Utility;

namespace NuGet.Tests.Apex
{
    internal class ApexTestContext : IDisposable
    {

        private VisualStudioHost _visualStudio;
        private readonly ILogger _logger;
        private SimpleTestPathContext _pathContext;

        public SolutionService SolutionService { get; }
        public ProjectTestExtension Project { get; }
        public string PackageSource => _pathContext.PackageSource;

        public NuGetApexTestService NuGetApexTestService { get; }

        public ApexTestContext(VisualStudioHost visualStudio, ProjectTemplate projectTemplate, ILogger logger, bool noAutoRestore = false)
        {
            logger.LogInformation("Creating test context");
            _pathContext = new SimpleTestPathContext();

            if (noAutoRestore)
            {
                _pathContext.Settings.DisableAutoRestore();
            }

            _visualStudio = visualStudio;
            _logger = logger;
            SolutionService = _visualStudio.Get<SolutionService>();
            NuGetApexTestService = _visualStudio.Get<NuGetApexTestService>();

            Project = CommonUtility.CreateAndInitProject(projectTemplate, _pathContext, SolutionService, logger);

            NuGetApexTestService.WaitForAutoRestore();
        }

        public void Dispose()
        {
            _logger.LogInformation("Test complete, closing solution.");
            SolutionService.Save();
            SolutionService.Close();

            _pathContext.Dispose();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Test.Apex.VisualStudio;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.Test.Utility;

namespace NuGet.Tests.Apex
{
    internal class ApexTestContext : IDisposable
    {

        private VisualStudioHost _visualStudio;
        private SolutionService _solutionService;
        private SimpleTestPathContext _pathContext;

        public ProjectTestExtension Project { get; }
        public string PackageSource => _pathContext.PackageSource;


        public ApexTestContext(VisualStudioHost visualStudio, ProjectTemplate projectTemplate)
        {
            _pathContext = new SimpleTestPathContext();
            _visualStudio = visualStudio;
            _solutionService = _visualStudio.Get<SolutionService>();

            Project = Utils.CreateAndInitProject(projectTemplate, _pathContext, _solutionService);
        }

        public void Dispose()
        {
            _solutionService.Save();
            _solutionService.Close();

            _pathContext.Dispose();
        }
    }
}

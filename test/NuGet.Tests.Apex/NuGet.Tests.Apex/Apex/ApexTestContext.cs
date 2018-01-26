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
        private SimpleTestPathContext _pathContext;

        public SolutionService SolutionService { get; }
        public ProjectTestExtension Project { get; }
        public string PackageSource => _pathContext.PackageSource;


        public ApexTestContext(VisualStudioHost visualStudio, ProjectTemplate projectTemplate)
        {
            _pathContext = new SimpleTestPathContext();
            _visualStudio = visualStudio;
            SolutionService = _visualStudio.Get<SolutionService>();

            Project = Utils.CreateAndInitProject(projectTemplate, _pathContext, SolutionService);
        }

        public void Dispose()
        {
            SolutionService.Save();
            SolutionService.Close();

            _pathContext.Dispose();
        }
    }
}

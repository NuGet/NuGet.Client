// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.Internal.VisualStudio.Shell.Embeddable.Feedback;
using NuGet.PackageManagement;

namespace NuGet.VisualStudio.Common
{
    [Export(typeof(IFeedbackDiagnosticFileProvider))]
    public class NuGetFeedbackDiagnosticFileProvider : IFeedbackDiagnosticFileProvider
    {
        public IReadOnlyCollection<string> GetFiles()
        {
            // todo: https://github.com/NuGet/Home/issues/8605

            return new List<string>()
            {
                DependencyGraphRestoreUtility.GetDefaultDGSpecFileName()
            };
        }
    }
}

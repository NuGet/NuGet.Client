// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;

namespace NuGet.CommandLine.XPlat
{
    internal class ListPackageJsonRendererV1 : ListPackageJsonRenderer
    {
        internal static ListPackageJsonRenderer GetInstance(TextWriter textWriter = null)
        {
            return new ListPackageJsonRendererV1(textWriter);
        }

        private ListPackageJsonRendererV1(TextWriter textWriter)
            : base(ReportOutputVersion.V1, textWriter)
        {
        }
    }
}

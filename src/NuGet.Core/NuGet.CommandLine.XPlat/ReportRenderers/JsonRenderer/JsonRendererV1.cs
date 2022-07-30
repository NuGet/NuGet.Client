// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.XPlat.ReportRenderers.JsonRenderer
{
    internal class JsonRendererV1 : JsonRenderer
    {
        internal JsonRendererV1()
            : base(ReportOutputVersion.V1)
        {
        }
    }
}

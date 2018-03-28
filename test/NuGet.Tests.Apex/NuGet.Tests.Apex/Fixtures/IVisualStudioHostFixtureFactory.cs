// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Tests.Foundation.TestAttributes.Context;

namespace NuGet.Tests.Apex
{
    /// <summary>
    /// Build a Visual Studio host fixture based on properties of a provided Context
    /// </summary>
    public interface IVisualStudioHostFixtureFactory
    {
        VisualStudioHostFixture GetVisualStudioHostFixture();
        VisualStudioHostFixture GetVisualStudioHostFixtureForContext(Context context);
    }
}

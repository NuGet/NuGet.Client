// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Threading;

internal static class CommonThreadCultureModule
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Initialize()
    {
        var enus = CultureInfo.GetCultureInfo("en-us");
        if (enus.LCID != CultureInfo.DefaultThreadCurrentCulture?.LCID)
        {
            CultureInfo.DefaultThreadCurrentCulture = enus;
            CultureInfo.DefaultThreadCurrentUICulture = enus;
        }
        if (enus.LCID != Thread.CurrentThread.CurrentCulture.LCID)
        {
            Thread.CurrentThread.CurrentCulture = enus;
            Thread.CurrentThread.CurrentUICulture = enus;
        }
    }
}

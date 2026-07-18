// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

internal static class CommonThreadCultureModule
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Initialize()
    {
        var enus = System.Globalization.CultureInfo.GetCultureInfo("en-us");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = enus;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = enus;
        System.Threading.Thread.CurrentThread.CurrentCulture = enus;
        System.Threading.Thread.CurrentThread.CurrentUICulture = enus;
    }
}

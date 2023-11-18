// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.PackageManagement.UI.ViewModels
{
    public class TrustedOwnerViewModel
    {
        public string Name { get; set; }
        public Uri Uri => !string.IsNullOrWhiteSpace(Name) ? new Uri($"https://www.nuget.org/profiles/{Name}") : null;

        public TrustedOwnerViewModel(string name)
        {
            Name = name;
        }
    }
}

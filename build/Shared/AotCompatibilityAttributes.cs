// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if NET9_0_OR_GREATER

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// This is a supporting forwarder for an internal polyfill API.
[assembly: TypeForwardedTo(typeof(FeatureSwitchDefinitionAttribute))]

#else

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    internal sealed class FeatureSwitchDefinitionAttribute : Attribute
    {
        public FeatureSwitchDefinitionAttribute(string switchName) => SwitchName = switchName;
        public string SwitchName { get; }
    }
}
#endif

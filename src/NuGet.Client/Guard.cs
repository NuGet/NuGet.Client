// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

// Why no Namespace? This is an internal type and we want it EVERYWHERE, so that's what we do.
internal static class Guard
{
    [ContractArgumentValidator]
    [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification="Code Contracts is tricking FxCop a little here :)")]
    internal static void NotNull(object value, string paramName) 
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName);
        }
        Contract.EndContractBlock();
    }

    [ContractArgumentValidator]
    [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Code Contracts is tricking FxCop a little here :)")]
    internal static void NotNullOrEmpty(string value, string paramName)
    {
        if (String.IsNullOrEmpty(value))
        {
            throw new ArgumentNullException(paramName);
        }
        Contract.EndContractBlock();
    }
}

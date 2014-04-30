using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

// Why no Namespace? This is an internal type and we want it EVERYWHERE, so that's what we do.
internal static class Guard
{
    [ContractAbbreviator]
    [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification="Code Contracts is tricking FxCop a little here :)")]
    internal static void NotNull(object value, string paramName) 
    {
        Contract.Requires<ArgumentNullException>(value != null, paramName);
    }

    [ContractAbbreviator]
    [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Code Contracts is tricking FxCop a little here :)")]
    internal static void NotNullOrEmpty(string value, string paramName)
    {
        Contract.Requires<ArgumentNullException>(!String.IsNullOrEmpty(value), paramName);
    }
}

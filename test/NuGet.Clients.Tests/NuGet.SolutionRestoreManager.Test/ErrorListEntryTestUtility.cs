// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Moq;

namespace NuGet.SolutionRestoreManager.Test
{
    internal static class ErrorListEntryTestUtility
    {
        /// <summary>
        /// Creates a mock Error List entry. Matching relies on the error <paramref name="code"/>;
        /// <paramref name="source"/> is only used to document that the source is irrelevant to matching
        /// (audit warnings surface with a "Build" source).
        /// </summary>
        internal static ITableEntryHandle CreateEntry(string? code, string? source = "NuGet")
        {
            Mock<ITableEntryHandle> entry = new();
            entry.Setup(e => e.TryGetValue(It.IsAny<string>(), out It.Ref<object>.IsAny))
                .Returns((string key, out object value) =>
                {
                    if (key == StandardTableColumnDefinitions.ErrorSource || key == StandardTableKeyNames.ErrorSource)
                    {
                        value = source!;
                        return source != null;
                    }

                    if (key == StandardTableKeyNames.ErrorCode)
                    {
                        value = code!;
                        return code != null;
                    }

                    value = null!;
                    return false;
                });

            return entry.Object;
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.SolutionRestoreManager.Test
{
    internal class VsProjectProperty : IVsProjectProperty
    {
        public string Name { get; }

        public string Value { get; }

        public VsProjectProperty(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(ProjectManagement.Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(name));
            }

            Name = name;
            Value = value;
        }
    }
}

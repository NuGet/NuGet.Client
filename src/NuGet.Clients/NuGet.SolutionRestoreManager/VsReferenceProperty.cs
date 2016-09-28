// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.SolutionRestoreManager
{
    internal class VsReferenceProperty : IVsReferenceProperty
    {
        public String Name { get; }

        public String Value { get; }

        public VsReferenceProperty(string name, string value)
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

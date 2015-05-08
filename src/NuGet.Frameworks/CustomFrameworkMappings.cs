// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Frameworks
{
    /// <summary>
    /// CustomFrameworkMappings adds framework mappings on top of the default mappings.
    /// </summary>
    public class CustomFrameworkMappings
    {
        public CustomFrameworkMappings(bool includeDefaultMappings)
        {
            throw new NotImplementedException();
        }

        public void AddIdentifierSynonym(string identifier, string synonym)
        {
            throw new NotImplementedException();
        }

        public void AddIdentifierShortName(string identifier, string shortName)
        {
            throw new NotImplementedException();
        }

        public void AddProfileShortName(string profile, string shortName)
        {
            throw new NotImplementedException();
        }
    }
}

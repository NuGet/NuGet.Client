// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.Protocol.Core.Types
{
    public class NullSourceCacheContext : SourceCacheContext
    {
        private static SourceCacheContext _instance;

        public static SourceCacheContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new NullSourceCacheContext();
                    _instance.DirectDownload = true;
                }

                return _instance;
            }
        }

        public override string GeneratedTempFolder
        {
            get
            {
                return string.Empty;
            }
        }

        public override SourceCacheContext WithRefreshCacheTrue() { return _instance; }

        public override SourceCacheContext Clone() { return _instance; }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;

namespace NuGet.Test.Utility
{
    public class CIOnlyPlatformFactAttribute : PlatformFactAttribute
    {
        private string _skip;

        public override string Skip
        {
            get
            {
                var skip = _skip;

                if (string.IsNullOrEmpty(skip))
                {
                    skip = base.Skip;
                }

                if (string.IsNullOrEmpty(skip))
                {
                    skip = XunitAttributeUtility.GetPlatformSkipMessageOrNull(GetAllPlatforms());
                }

                if (string.IsNullOrEmpty(skip))
                {
                    skip = XunitAttributeUtility.GetMonoMessage(OnlyOnMono, SkipMono);
                }

                // If this is null the test will run.
                return skip;
            }

            set => _skip = value;
        }

        /// <summary>
        /// Run only on the given platforms
        /// </summary>
        public CIOnlyPlatformFactAttribute(params string[] platforms)
        {
            Platforms = platforms.ToList();
        }
    }
}

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Test.TestExtensions.TestablePlugin
{
    internal sealed class Arguments
    {
        internal ushort PortNumber { get; private set; }

        private Arguments() { }

        internal static bool TryParse(IReadOnlyList<string> args, out Arguments arguments)
        {
            arguments = null;

            ushort portNumber = 0;
            var isPlugin = false;

            for (var i = 0; i < args.Count; ++i)
            {
                var flag = args[i];

                switch (flag.ToLower())
                {
                    case "-portnumber":
                        if (i + 1 == args.Count)
                        {
                            return false;
                        }

                        if (!ushort.TryParse(args[++i], out portNumber))
                        {
                            return false;
                        }
                        break;

                    case "-plugin":
                        isPlugin = true;
                        break;

                    default:
                        return false;
                }
            }

            if (!isPlugin)
            {
                return false;
            }

            arguments = new Arguments()
            {
                PortNumber = portNumber
            };

            return true;
        }
    }
}
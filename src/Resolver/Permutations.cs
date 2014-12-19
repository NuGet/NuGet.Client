using NuGet.Versioning;
using System;
using System.Collections.Generic;

namespace NuGet.Resolver
{
    class Permutations
    {
        static bool Loop(IList<KeyValuePair<string, NuGetVersion>>[] x, int i, Stack<KeyValuePair<string, NuGetVersion>> candidate, Func<IEnumerable<KeyValuePair<string, NuGetVersion>>, bool> test)
        {
            if (i == x.Length)
            {
                return test(candidate);
            }

            foreach (KeyValuePair<string, NuGetVersion> s in x[i])
            {
                candidate.Push(s);
                if (Loop(x, i + 1, candidate, test))
                {
                    return true;
                }
                candidate.Pop();
            }

            return false;
        }

        public static void Run(IList<KeyValuePair<string, NuGetVersion>>[] x, Func<IEnumerable<KeyValuePair<string, NuGetVersion>>, bool> test)
        {
            Stack<KeyValuePair<string, NuGetVersion>> candidate = new Stack<KeyValuePair<string, NuGetVersion>>();
            Loop(x, 0, candidate, test);
        }
    }
}

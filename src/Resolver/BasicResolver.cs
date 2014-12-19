using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Resolver
{
    public static class BasicResolver
    {
        public static IList<PackageReference> Solve(RegistrationInfo registrationInfo)
        {
            // Finding a solution is a two step process:

            // Step 1. The passed in RegistrationInfo is a complete tree of all the package metadata that could take part in the resolution
            //         This first step creates a flattened plan structure from this tree. The plan consists of a an array of lists where
            //         each list contains the possible versions of a particular package registration. Every package that is up for
            //         consideration in the resolution is contained in these lists. The order within each list represents the oreder in which
            //         packages are considered (for example highest version number at the top etc.) and the order of the lists in teh array
            //         dictates teh order in which the data set is iterated.

            IList<KeyValuePair<string, NuGetVersion>>[] plan = CreatePlan(registrationInfo);

            // Step 2. The plan is simply permuted - that is every permutation of the packages is tried back against the tree. If the permutation
            //         satisfies the constraint implied by the tree then it is a solution. The loop stops at the first solution it finds.

            int iterations = 0;
            
            IEnumerable<KeyValuePair<string, NuGetVersion>> solution = Execute(registrationInfo, plan, out iterations);

            // TODO: Determine the real framework to install
            IList<PackageReference> result = solution.Select(entry => new PackageReference(new PackageIdentity(entry.Key, entry.Value), NuGetFramework.AnyFramework)).ToList();

            Console.WriteLine("actual iterations: {0}", iterations);

            return result;
        }

        public static IEnumerable<KeyValuePair<string, NuGetVersion>> Execute(RegistrationInfo registrationInfo, IList<KeyValuePair<string, NuGetVersion>>[] plan, out int iterations)
        {
            IEnumerable<KeyValuePair<string, NuGetVersion>> solution = null;

            int i = 0;

            Permutations.Run(plan, (IEnumerable<KeyValuePair<string, NuGetVersion>> candidate) =>
            {
                i++;

                if (Evaluate.Satisfy(registrationInfo, candidate))
                {
                    solution = candidate;
                    return true;
                }

                return false;
            });

            iterations = i;

            return solution;
        }

        public static IList<KeyValuePair<string, NuGetVersion>>[] CreatePlan(RegistrationInfo registrationInfo)
        {
            IDictionary<string, IList<NuGetVersion>> participants = Participants.GetParticipants(registrationInfo);

            IList<KeyValuePair<string, NuGetVersion>>[] plan = Participants.CreatePlan(participants);

            return plan;
        }
    }
}

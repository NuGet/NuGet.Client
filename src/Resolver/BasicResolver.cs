using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Resolver
{
    public static class BasicResolver
    {
        public static IList<PackageEntry> Solve(RegistrationInfo registrationInfo)
        {
            IList<KeyValuePair<string, NuGetVersion>>[] plan = BasicResolver.CreatePlan(registrationInfo);

            int iterations = 0;

            IEnumerable<KeyValuePair<string, NuGetVersion>> solution = BasicResolver.Execute(registrationInfo, plan, out iterations);

            IList<PackageEntry> result = solution.Select(entry => new PackageEntry(entry.Key, entry.Value)).ToList();

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

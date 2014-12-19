using NuGet.Versioning;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Resolver
{
    class Participants
    {
        public static IDictionary<string, IList<NuGetVersion>> GetParticipants(RegistrationInfo registrationInfo)
        {
            IDictionary<string, ISet<NuGetVersion>> participants = new Dictionary<string, ISet<NuGetVersion>>();

            GetParticipants(registrationInfo, participants);

            IDictionary<string, IList<NuGetVersion>> result = new Dictionary<string, IList<NuGetVersion>>();

            foreach (KeyValuePair<string, ISet<NuGetVersion>> participant in participants)
            {
                result[participant.Key] = participant.Value.OrderBy(v => v).Reverse().ToList();
            }

            return result;
        }

        private static void GetParticipants(PackageInfo packageInfo, string id, IDictionary<string, ISet<NuGetVersion>> participants)
        {
            if (id != null)
            {
                ISet<NuGetVersion> versions;
                if (!participants.TryGetValue(id, out versions))
                {
                    versions = new HashSet<NuGetVersion>();
                    participants.Add(id, versions);
                }

                versions.Add(packageInfo.Version);
            }

            foreach (DependencyGroupInfo dependencyGroupInfo in packageInfo.DependencyGroups)
            {
                foreach (DependencyInfo dependencyInfo in dependencyGroupInfo.Dependencies)
                {
                    GetParticipants(dependencyInfo.RegistrationInfo, participants);
                }
            }
        }

        private static void GetParticipants(RegistrationInfo registrationInfo, IDictionary<string, ISet<NuGetVersion>> participants)
        {
            foreach (PackageInfo child in registrationInfo.Packages)
            {
                GetParticipants(child, registrationInfo.Id, participants);
            }
        }

        //

        public static IList<KeyValuePair<string, NuGetVersion>>[] CreatePlan(IDictionary<string, IList<NuGetVersion>> participants)
        {
            IList<KeyValuePair<string, NuGetVersion>>[] plan = new List<KeyValuePair<string, NuGetVersion>>[participants.Count];
            int i = 0;

            foreach (KeyValuePair<string, IList<NuGetVersion>> participant in participants)
            {
                IList<KeyValuePair<string, NuGetVersion>> packages = new List<KeyValuePair<string, NuGetVersion>>();

                foreach (NuGetVersion version in participant.Value)
                {
                    packages.Add(new KeyValuePair<string, NuGetVersion>(participant.Key, version));
                }

                plan[i++] = packages;
            }

            return plan;
        }
    }
}

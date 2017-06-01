// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Common
{

    /// <summary>
    /// Contains Package specific properties for Warnings.
    /// </summary>
    public class PackageSpecificWarningProperties
    {
        private const string _globalTFM = "Global";

        /// <summary>
        /// Contains Package specific No warn properties.
        /// NuGetLogCode -> LibraryId -> Set of TargerGraphs.
        /// </summary>
        private IDictionary<NuGetLogCode, IDictionary<string, ISet<string>>> Properties;

        /// <summary>
        /// Adds a NuGetLogCode into the NoWarn Set for the specified library Id and target graph.
        /// </summary>
        /// <param name="code">NuGetLogCode for which no warning should be thrown.</param>
        /// <param name="libraryId">Library for which no warning should be thrown.</param>
        /// <param name="targetGraph">Target graph for which no warning should be thrown.</param>
        public void Add(NuGetLogCode code, string libraryId, string targetGraph)
        {
            if (Properties == null)
            {
                Properties = new Dictionary<NuGetLogCode, IDictionary<string, ISet<string>>>();
            }

            if (!Properties.ContainsKey(code))
            {
                Properties.Add(code, new Dictionary<string, ISet<string>>());
            }

            if (Properties[code].ContainsKey(libraryId))
            {
                Properties[code][libraryId].Add(targetGraph);
            }
            else
            {
                Properties[code].Add(libraryId, new HashSet<string> { targetGraph });
            }
        }

        /// <summary>
        /// Adds a NuGetLogCode into the NoWarn Set for the specified library Id with unconditional reference.
        /// </summary>
        /// <param name="code">NuGetLogCode for which no warning should be thrown.</param>
        /// <param name="libraryId">Library for which no warning should be thrown.</param>
        public void Add(NuGetLogCode code, string libraryId)
        {
            Add(code, libraryId, _globalTFM);
        }

        /// <summary>
        /// Adds a list of NuGetLogCode into the NoWarn Set for the specified library Id and target graph.
        /// </summary>
        /// <param name="codes">IEnumerable of NuGetLogCode for which no warning should be thrown.</param>
        /// <param name="libraryId">Library for which no warning should be thrown.</param>
        /// <param name="targetGraph">Target graph for which no warning should be thrown.</param>
        public void AddRange(IEnumerable<NuGetLogCode> codes, string libraryId, string targetGraph)
        {
            foreach (var code in codes)
            {
                Add(code, libraryId, targetGraph);
            }
        }

        /// <summary>
        /// Adds a list of NuGetLogCode into the NoWarn Set for the specified library Id with unconditional reference.
        /// </summary>
        /// <param name="codes">IEnumerable of NuGetLogCode for which no warning should be thrown.</param>
        /// <param name="libraryId">Library for which no warning should be thrown.</param>
        public void AddRange(IEnumerable<NuGetLogCode> codes, string libraryId)
        {
            foreach (var code in codes)
            {
                Add(code, libraryId, _globalTFM);
            }
        }


        /// <summary>
        /// Checks if a NugetLogCode is part of the NoWarn list for the specified library Id and target graph.
        /// </summary>
        /// <param name="code">NugetLogCode to be checked.</param>
        /// <param name="libraryId">library Id to be checked.</param>
        /// <param name="targetGraph">target graph to be checked.</param>
        /// <returns>True iff the NugetLogCode is part of the NoWarn list for the specified libraryId and Target Graph.</returns>
        public bool Contains(NuGetLogCode code, string libraryId, string targetGraph)
        {
            return Properties != null &&
                Properties.ContainsKey(code) &&
                Properties[code].ContainsKey(libraryId) &&
                Properties[code][libraryId].Contains(targetGraph);
        }

        /// <summary>
        /// Checks if a NugetLogCode is part of the NoWarn list for the specified library Id with uncoditional reference.
        /// </summary>
        /// <param name="code">NugetLogCode to be checked.</param>
        /// <param name="libraryId">library Id to be checked.</param>
        /// <returns>True iff the NugetLogCode is part of the NoWarn list for the specified libraryId with uncoditional reference.</returns>
        public bool Contains(NuGetLogCode code, string libraryId)
        {
            return Properties != null &&
                Properties.ContainsKey(code) &&
                Properties[code].ContainsKey(libraryId) &&
                Properties[code][libraryId].Contains(_globalTFM);
        }
    }
}

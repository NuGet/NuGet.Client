// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Globalization;
using System.Linq;


namespace NuGet.Build.Tasks
{
    public class CheckForDuplicateNuGetItems : Task
    {
        [Required]
        public ITaskItem[] Items { get; set; }

        [Required]
        public string ItemName { get; set; }

        [Output]
        public ITaskItem[] DeduplicatedItems { get; set; }

        public override bool Execute()
        {
            DeduplicatedItems = Array.Empty<ITaskItem>();

            var itemGroups = Items.GroupBy(i => i.ItemSpec);

            var duplicateItems = itemGroups.Where(g => g.Count() > 1).ToList();
            var error = false;
            if (duplicateItems.Any())
            {
                error = true;
                string duplicateItemsFormatted = string.Join("; ", duplicateItems.Select(d => string.Join(", ", d.Select(e => $"{e.ItemSpec} {e.GetMetadata("version")}"))));

                string message = string.Format(CultureInfo.CurrentCulture, "Duplicate '{0}' items were discovered. Remove the duplicate items or use the Update functionality to ensure a consistent restore behavior. Duplicate '{0}' list: {1}",
                    ItemName,
                    duplicateItemsFormatted);

                Log.LogError(message);

                DeduplicatedItems = itemGroups.Select(g => g.Last()).ToArray();
            }

            return !error;
        }
    }
}

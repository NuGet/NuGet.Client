// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Common;
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

        [Required]
        public string LogCode { get; set; }

        [Output]
        public ITaskItem[] DeduplicatedItems { get; set; }

        [Output]
        public bool AnyItemsDeduplicated { get; set; }

        public override bool Execute()
        {
            var log = new MSBuildLogger(Log);

            DeduplicatedItems = Array.Empty<ITaskItem>();
            var itemGroups = Items.GroupBy(i => i.ItemSpec, StringComparer.OrdinalIgnoreCase);
            var duplicateItems = itemGroups.Where(g => g.Count() > 1).ToList();

            if (duplicateItems.Any())
            {
                AnyItemsDeduplicated = true;
                string duplicateItemsFormatted = string.Join("; ", duplicateItems.Select(d => string.Join(", ", d.Select(e => $"{e.ItemSpec} {e.GetMetadata("version")}"))));
                var logCode = Enum.Parse(typeof(NuGetLogCode), LogCode);
                var logMessage = new RestoreLogMessage(
                    LogLevel.Error,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Error_DuplicateItems,
                        logCode,
                        ItemName,
                        duplicateItemsFormatted));

                log.Log(logMessage);
                DeduplicatedItems = itemGroups.Select(g => g.First()).ToArray();
            }

            return !AnyItemsDeduplicated;
        }
    }
}

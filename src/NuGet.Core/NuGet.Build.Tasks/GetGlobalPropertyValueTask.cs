// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using NuGet.Packaging;
using Task = Microsoft.Build.Utilities.Task;


namespace NuGet.Build.Tasks
{
    public class GetGlobalPropertyValueTask : Task
    {
        [Required]
        public string PropertyName { get; set; }

        [Output]
        public string GlobalPropertyValue { get; set; }

        public override bool Execute()
        {
            var globalProperties = GetGlobalProperties();


            if (globalProperties.TryGetValue(PropertyName, out string globalProperty))
            {
                GlobalPropertyValue = globalProperty;
            }
            return !Log.HasLoggedErrors;
        }
        internal Dictionary<string, string> GetGlobalProperties()
        {
#if IS_CORECLR
            // MSBuild 16.5 and above has a method to get the global properties, older versions do not
            Dictionary<string, string> msBuildGlobalProperties = BuildEngine is IBuildEngine6 buildEngine6
                ? buildEngine6.GetGlobalProperties().ToDictionary(i => i.Key, i => i.Value, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
#else
            var msBuildGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // MSBuild 16.5 added a new interface, IBuildEngine6, which has a GetGlobalProperties() method.  However, we compile against
            // Microsoft.Build.Framework version 4.0 when targeting .NET Framework, so reflection is required since type checking
            // can't be done at compile time
            var buildEngine6Type = typeof(IBuildEngine).Assembly.GetType("Microsoft.Build.Framework.IBuildEngine6");

            if (buildEngine6Type != null)
            {
                var getGlobalPropertiesMethod = buildEngine6Type.GetMethod("GetGlobalProperties", BindingFlags.Instance | BindingFlags.Public);

                if (getGlobalPropertiesMethod != null)
                {
                    try
                    {
                        if (getGlobalPropertiesMethod.Invoke(BuildEngine, null) is IReadOnlyDictionary<string, string> globalProperties)
                        {
                            msBuildGlobalProperties.AddRange(globalProperties);
                        }
                    }
                    catch (Exception)
                    {
                        // Ignored
                    }
                }
            }
#endif
            return msBuildGlobalProperties;
        }
    }
}

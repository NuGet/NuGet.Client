// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// This class is used to hold restore related, project specific settings.
    /// </summary>
    public class ProjectRestoreSettings
    {
        /// <summary>
        /// Bool property is used inr estore command to not log errors and warning.
        /// Currently this is only being used for net core based projects on nomination.
        /// </summary>
        public bool HideWarningsAndErrors { get; set; } = false;

        public ProjectRestoreSettings Clone()
        {
            var clonedObject = new ProjectRestoreSettings();
            clonedObject.HideWarningsAndErrors = HideWarningsAndErrors;
            return clonedObject;
        }
    }
}

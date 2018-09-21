// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics.CodeAnalysis;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TemplateWizard;

namespace NuGet.VisualStudio
{
    [SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses",
        Justification = "This class is referenced via .vstemplate files from a VS project template")]
    internal sealed class TemplateWizard : IWizard
    {
        [Import]
        internal IVsTemplateWizard Wizard { get; set; }

        private void Initialize(object automationObject)
        {
            using (var serviceProvider = new ServiceProvider((IServiceProvider)automationObject))
            {
                var componentModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));

                using (var container = new CompositionContainer(componentModel.DefaultExportProvider))
                {
                    container.ComposeParts(this);
                }
            }
        }

        void IWizard.BeforeOpeningFile(ProjectItem projectItem)
        {
            Wizard.BeforeOpeningFile(projectItem);
        }

        void IWizard.ProjectFinishedGenerating(Project project)
        {
            Wizard.ProjectFinishedGenerating(project);
        }

        void IWizard.ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
            Wizard.ProjectItemFinishedGenerating(projectItem);
        }

        void IWizard.RunFinished()
        {
            Wizard.RunFinished();
        }

        void IWizard.RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            Initialize(automationObject);

            Wizard.RunStarted(automationObject, replacementsDictionary, runKind, customParams);
        }

        bool IWizard.ShouldAddProjectItem(string filePath)
        {
            return Wizard.ShouldAddProjectItem(filePath);
        }
    }
}

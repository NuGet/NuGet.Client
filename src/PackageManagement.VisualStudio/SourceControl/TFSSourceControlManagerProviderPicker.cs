// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Reflection;
using EnvDTE80;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(ITFSSourceControlManagerProvider))]
    public class TFSSourceControlManagerProviderPicker : ITFSSourceControlManagerProvider
    {
        private const string typeName = "NuGet.TeamFoundationServer.TFSSourceControlManagerProvider";
        private ITFSSourceControlManagerProvider _cachedTFSSourceControlManagerProvider;

        public SourceControlManager GetTFSSourceControlManager(SourceControlBindings sourceControlBindings)
        {
            var underlyingTfsProvider = GetUnderlyingTfsProvider();
            return underlyingTfsProvider != null ? underlyingTfsProvider.GetTFSSourceControlManager(sourceControlBindings) : null;
        }

        private ITFSSourceControlManagerProvider GetUnderlyingTfsProvider()
        {
            if (_cachedTFSSourceControlManagerProvider == null)
            {
                string assemblyName;
#if VS12
                assemblyName = "NuGet.TeamFoundationServer12";
#endif

#if VS14
                assemblyName = "NuGet.TeamFoundationServer14";
#endif
                try
                {
                    Assembly assembly = RuntimeHelpers.LoadAssemblySmart(assemblyName);

                    if (assembly != null)
                    {
                        var type = assembly.GetType(typeName, throwOnError: false);
                        if (type != null)
                        {
                            _cachedTFSSourceControlManagerProvider = (ITFSSourceControlManagerProvider)Activator.CreateInstance(type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExceptionHelper.WriteToActivityLog(ex);
                    _cachedTFSSourceControlManagerProvider = null;
                }
            }
            return _cachedTFSSourceControlManagerProvider;
        }
    }
}

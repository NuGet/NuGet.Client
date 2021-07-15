// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Experimentation;
using NuGet.Common;

namespace NuGet.VisualStudio
{
    [Export(typeof(INuGetExperimentationService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class NuGetExperimentationService : INuGetExperimentationService
    {
        private readonly IEnvironmentVariableReader _environmentVariableReader;
        private readonly IExperimentationService _experimentationService;

        public NuGetExperimentationService()
            : this(EnvironmentVariableWrapper.Instance, ExperimentationService.Default)
        {
            // ensure uniqueness.
        }

        internal NuGetExperimentationService(IEnvironmentVariableReader environmentVariableReader, IExperimentationService experimentationService)
        {
            _environmentVariableReader = environmentVariableReader ?? throw new ArgumentNullException(nameof(environmentVariableReader));
            _experimentationService = experimentationService ?? throw new ArgumentNullException(nameof(experimentationService));
        }

        public bool IsExperimentEnabled(ExperimentationConstants experimentation)
        {
            var isEnvVarEnabled = !string.IsNullOrEmpty(experimentation.FlightEnvironmentVariable)
                && _environmentVariableReader.GetEnvironmentVariable(experimentation.FlightEnvironmentVariable) == "1";

            return isEnvVarEnabled || _experimentationService.IsCachedFlightEnabled(experimentation.FlightFlag);
        }
    }
}

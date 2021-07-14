// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Experimentation;
using NuGet.Common;

namespace NuGet.VisualStudio
{
    public class NuGetExperimentationService
    {
        public static readonly NuGetExperimentationService Instance = new();

        private readonly IEnvironmentVariableReader _environmentVariableReader;
        private readonly IExperimentationService _experimentationService;
        internal NuGetExperimentationService()
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

            return _environmentVariableReader.GetEnvironmentVariable(experimentation.FlightEnvironmentVariable) == "1"
                || _experimentationService.IsCachedFlightEnabled(experimentation.FlightFlag);
        }
    }
}

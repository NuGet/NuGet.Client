// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Moq;
using NuGet.ProjectManagement;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class ActionsTelemetryServiceTests
    {
        [Theory]
        [InlineData(NuGetOperationType.Install, OperationSource.UI)]
        [InlineData(NuGetOperationType.Update, OperationSource.UI)]
        [InlineData(NuGetOperationType.Uninstall, OperationSource.UI)]
        [InlineData(NuGetOperationType.Install, OperationSource.PMC)]
        [InlineData(NuGetOperationType.Update, OperationSource.PMC)]
        [InlineData(NuGetOperationType.Uninstall, OperationSource.PMC)]
        public void ActionsTelemetryService_EmitActionEvent_OperationSucceed(NuGetOperationType operationType, OperationSource source)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            string operationId = Guid.NewGuid().ToString();

            var actionTelemetryData = new ActionsTelemetryEvent(
                operationId: operationId,
                projectIds: new[] { Guid.NewGuid().ToString() },
                operationType: operationType,
                source: source,
                startTime: DateTimeOffset.Now.AddSeconds(-3),
                status: NuGetOperationStatus.Succeeded,
                packageCount: 1,
                endTime: DateTimeOffset.Now,
                duration: 2.10);
            var service = new ActionsTelemetryService(telemetrySession.Object);

            // Act
            service.EmitActionEvent(actionTelemetryData, null);

            // Assert
            VerifyTelemetryEventData(actionTelemetryData, lastTelemetryEvent);
        }

        [Theory]
        [InlineData(NuGetOperationType.Install, OperationSource.UI)]
        [InlineData(NuGetOperationType.Update, OperationSource.UI)]
        [InlineData(NuGetOperationType.Uninstall, OperationSource.UI)]
        [InlineData(NuGetOperationType.Install, OperationSource.PMC)]
        [InlineData(NuGetOperationType.Update, OperationSource.PMC)]
        [InlineData(NuGetOperationType.Uninstall, OperationSource.PMC)]
        public void ActionsTelemetryService_EmitActionEvent_OperationFailed(NuGetOperationType operationType, OperationSource source)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            string operationId = Guid.NewGuid().ToString();

            var actionTelemetryData = new ActionsTelemetryEvent(
                operationId: operationId,
                projectIds: new[] { Guid.NewGuid().ToString() },
                operationType: operationType,
                source: source,
                startTime: DateTimeOffset.Now.AddSeconds(-2),
                status: NuGetOperationStatus.Failed,
                packageCount: 1,
                endTime: DateTimeOffset.Now,
                duration: 1.30);
            var service = new ActionsTelemetryService(telemetrySession.Object);

            // Act
            service.EmitActionEvent(actionTelemetryData, null);

            // Assert
            VerifyTelemetryEventData(actionTelemetryData, lastTelemetryEvent);
        }

        [Theory]
        [InlineData(NuGetOperationType.Install)]
        [InlineData(NuGetOperationType.Update)]
        [InlineData(NuGetOperationType.Uninstall)]
        public void ActionsTelemetryService_EmitActionEvent_OperationNoOp(NuGetOperationType operationType)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            string operationId = Guid.NewGuid().ToString();

            var actionTelemetryData = new ActionsTelemetryEvent(
                operationId: operationId,
                projectIds: new[] { Guid.NewGuid().ToString() },
                operationType: operationType,
                source: OperationSource.PMC,
                startTime: DateTimeOffset.Now.AddSeconds(-1),
                status: NuGetOperationStatus.NoOp,
                packageCount: 1,
                endTime: DateTimeOffset.Now,
                duration: .40);
            var service = new ActionsTelemetryService(telemetrySession.Object);

            // Act
            service.EmitActionEvent(actionTelemetryData, null);

            // Assert
            VerifyTelemetryEventData(actionTelemetryData, lastTelemetryEvent);
        }

        [Theory]
        [MemberData(nameof(GetStepNames))]
        public void ActionsTelemetryService_EmitActionStepsEvent(string stepName)
        {
            // Arrange
            var telemetrySession = new Mock<ITelemetrySession>();
            TelemetryEvent lastTelemetryEvent = null;
            telemetrySession
                .Setup(x => x.PostEvent(It.IsAny<TelemetryEvent>()))
                .Callback<TelemetryEvent>(x => lastTelemetryEvent = x);

            string operationId = Guid.NewGuid().ToString();
            var duration = 1.12;
            var stepNameWithProject = string.Format(stepName, "testProject");
            var service = new ActionsTelemetryService(telemetrySession.Object);

            // Act
            service.EmitActionStepsEvent(operationId, stepNameWithProject, duration);

            // Assert
            Assert.NotNull(lastTelemetryEvent);
            Assert.Equal(TelemetryConstants.NugetActionStepsEventName, lastTelemetryEvent.Name);
            Assert.Equal(3, lastTelemetryEvent.Properties.Count);

            Assert.Equal(operationId, lastTelemetryEvent.Properties[TelemetryConstants.OperationIdPropertyName].ToString());
            Assert.Equal(stepNameWithProject, lastTelemetryEvent.Properties[TelemetryConstants.StepNamePropertyName].ToString());
            Assert.Equal(duration, (double)lastTelemetryEvent.Properties[TelemetryConstants.DurationPropertyName]);
        }

        public static IEnumerable<object[]> GetStepNames()
        {
            yield return new[] { TelemetryConstants.PreviewBuildIntegratedStepName };
            yield return new[] { TelemetryConstants.GatherDependencyStepName };
            yield return new[] { TelemetryConstants.ResolveDependencyStepName };
            yield return new[] { TelemetryConstants.ResolvedActionsStepName };
            yield return new[] { TelemetryConstants.ExecuteActionStepName };
        }

        private void VerifyTelemetryEventData(ActionsTelemetryEvent expected, TelemetryEvent actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(TelemetryConstants.NugetActionEventName, actual.Name);
            Assert.Equal(10, actual.Properties.Count);

            Assert.Equal(expected.OperationType.ToString(), actual.Properties[TelemetryConstants.OperationTypePropertyName].ToString());
            Assert.Equal(expected.Source.ToString(), actual.Properties[TelemetryConstants.OperationSourcePropertyName].ToString());

            TestTelemetryUtility.VerifyTelemetryEventData(expected, actual);
        }
    }
}

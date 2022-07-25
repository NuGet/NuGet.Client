// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.CommandLine.XPlat.ReportRenderers
{
    internal class JsonRenderer : IReportRenderer
    {
        public JsonRenderer()
        {

        }

        public void EventPipeSourceConnected()
        {
            throw new System.NotImplementedException();
        }

        public void Initialize()
        {
            throw new System.NotImplementedException();
        }

        public void ReportPayloadReceived(string payload)
        {
            throw new System.NotImplementedException();
        }

        public void SetErrorText(string errorText)
        {
            throw new System.NotImplementedException();
        }

        public void Stop()
        {
            throw new System.NotImplementedException();
        }

        public void ToggleStatus(bool paused)
        {
            throw new System.NotImplementedException();
        }
    }
}

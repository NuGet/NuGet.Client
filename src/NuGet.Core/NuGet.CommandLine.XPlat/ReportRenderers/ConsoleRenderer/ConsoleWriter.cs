// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.CommandLine.XPlat.ReportRenderers.Enums;
using NuGet.CommandLine.XPlat.ReportRenderers.Interfaces;
using NuGet.CommandLine.XPlat.ReportRenderers.Models;

namespace NuGet.CommandLine.XPlat.ReportRenderers.ConsoleRenderer
{
    internal class ConsoleWriter : IReportRenderer
    {
        protected ListPackageReportModel _listPackageReportModel;

        internal string Parameters { get; private set; }

        private ConsoleWriter()
        { }

        public static ConsoleWriter Instance { get; } = new ConsoleWriter();

        public void AddProblem(string errorText, ProblemType problemType)
        {
            switch (problemType)
            {
                case ProblemType.Information:
                    Console.WriteLine(errorText);
                    break;
                case ProblemType.Debug:
                    Console.WriteLine(errorText);
                    break;
                case ProblemType.Warning:
                    Console.WriteLine(errorText);
                    break;
                case ProblemType.Error:
                    Console.Error.WriteLine(errorText);
                    break;
                default:
                    break;
            }
        }

        public void Write(ListPackageReportModel listPackageReportModel)
        {
            _listPackageReportModel = listPackageReportModel;
        }

        public void End()
        {
            //DisplayMessages(_listPackageReportModel.ListPackageArgs);
        }

        public void SetParameters(string parametersText)
        {
            Parameters = parametersText;
        }
    }
}

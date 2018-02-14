// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Xsl;

namespace NuGet.PackageManagement.UI
{
    internal class UpgradeLogger
    {
        // Example upgrade report XML:
        //
        // <?xml version="1.0" encoding="UTF-8"?>
        // <NuGetUpgradeReport Name="ConsoleApplication13" BackupPath="c:\MySolution\Backup\ConsoleApplication13">
        //   <Properties />
        //   <Projects>
        //     <Project Name="ConsoleApplication13">
        //       <Issues>
        //         <Issue Level="1" Description="Newtonsoft.Json.6.0.8 contains an install.ps1 script that will not be applied after upgrading." />
        //         <Issue Level="2" Description="My.God.Its.Full.Of.Stars.1.4.9 could not be found." />
        //       </Issues>
        //       <IncludedPackages>
        //         <Package Name="Microsoft.Owin.3.0.1" />
        //         <Package Name="WindowsAzure.Storage.7.0.0" />
        //       </IncludedPackages>
        //       <ExcludedPackages>
        //         <Package Name="Microsoft.Azure.KeyVault.Core.1.0.0 (dependency of WindowsAzure.Storage.7.0.0)" />
        //         <Package Name="Microsoft.Data.Edm.5.6.4 (dependency of Microsoft.Data.OData.5.6.4)" />
        //         <Package Name="Microsoft.Data.OData.5.6.4 (dependency of Microsoft.Data.Services.Client.5.6.4, WindowsAzure.Storage.7.0.0)" />
        //         <Package Name="Microsoft.Data.Services.Client.5.6.4 (dependency of WindowsAzure.Storage.7.0.0)" />
        //         <Package Name="Newtonsoft.Json.6.0.8 (dependency of WindowsAzure.Storage.7.0.0)" />
        //         <Package Name="Owin.1.0.0 (dependency of Microsoft.Owin.3.0.1)" />
        //         <Package Name="System.Spatial.5.6.4 (dependency of Microsoft.Data.OData.5.6.4)" />
        //       </ExcludedPackages>
        //     </Project>
        //   </Projects>
        // </NuGetUpgradeReport>
        //
        // Note that we currently only supported upgrading a single project, but the log format can handle multiple projects
        // (in which case the log name would likely be the name of the solution).

        private const string DescriptionString = "Description";
        private const string ExcludedPackagesString = "ExcludedPackages";
        private const string IncludedPackagesString = "IncludedPackages";
        private const string IssuesString = "Issues";
        private const string IssueString = "Issue";
        private const string LevelString = "Level";
        private const string NameString = "Name";
        private const string NuGetUpgradeReportString = "NuGetUpgradeReport";
        private const string PackageString = "Package";
        private const string ProjectsString = "Projects";
        private const string ProjectString = "Project";
        private const string PropertiesString = "Properties";
        private const string PropertyString = "Property";
        private const string ValueString = "Value";

        private const string XsltManifestResourceName = "NuGet.PackageManagement.UI.Resources.UpgradeReport.xslt";

        private readonly ConcurrentDictionary<string, XmlElement> _projectElements = new ConcurrentDictionary<string, XmlElement>();

        private readonly XmlDocument _xmlDocument;

        private readonly XmlElement _projectsElement;
        private readonly XmlElement _propertiesElement;

        private readonly string _backupPath;

        internal UpgradeLogger(string reportName, string backupPath)
        {
            if (string.IsNullOrEmpty(reportName))
            {
                throw new ArgumentException(nameof(reportName));
            }

            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
            {
                throw new ArgumentException(Resources.UpgradeLogger_BackupPathMustBeValid, nameof(backupPath));
            }

            _backupPath = backupPath;

            _xmlDocument = new XmlDocument { PreserveWhitespace = true };
            _xmlDocument.LoadXml($"<?xml version='1.0' encoding='UTF-16'?>\r\n<{NuGetUpgradeReportString}>\r\n</{NuGetUpgradeReportString}>");

            var upgradeReportElement = _xmlDocument.DocumentElement;
            Debug.Assert(upgradeReportElement != null, "_upgradeReportElement != null");

            upgradeReportElement.SetAttribute("Name", reportName);
            upgradeReportElement.SetAttribute("BackupPath", backupPath);

            _propertiesElement = _xmlDocument.CreateElement(PropertiesString);
            upgradeReportElement.AppendChild(_propertiesElement);

            _projectsElement = _xmlDocument.CreateElement(ProjectsString);
            upgradeReportElement.AppendChild(_projectsElement);
        }

        internal void SetProperty(string propertyName, string propertyValue)
        {
            var propertyElement = _xmlDocument.CreateElement(PropertyString);
            propertyElement.SetAttribute(NameString, propertyName);
            propertyElement.SetAttribute(ValueString, propertyValue);
            _propertiesElement.AppendChild(propertyElement);
        }

        internal void LogIssue(string projectName, ErrorLevel errorLevel, string description)
        {
            var issueElement = _xmlDocument.CreateElement(IssueString);
            issueElement.SetAttribute(LevelString, ((uint)errorLevel).ToString());
            issueElement.SetAttribute(DescriptionString, description);

            var issuesElement = GetProjectElement(projectName).SelectSingleNode(IssuesString);
            Debug.Assert(issuesElement != null, "issuesElement != null");
            issuesElement.AppendChild(issueElement);
        }

        internal void RegisterPackage(string projectName, string name, bool included)
        {
            var packageElement = _xmlDocument.CreateElement(PackageString);
            packageElement.SetAttribute(NameString, name);

            var packagesElement = GetProjectElement(projectName).SelectSingleNode(included ? IncludedPackagesString : ExcludedPackagesString);
            Debug.Assert(packagesElement != null, "packagesElement != null");
            packagesElement.AppendChild(packageElement);
        }

        internal string Flush()
        {
            var htmlFilePath = $@"{_backupPath}\NuGetUpgradeLog.html";
            using (var xsltStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(XsltManifestResourceName))
            {
                Debug.Assert(xsltStream != null, $"Resource {XsltManifestResourceName} could not be loaded.");

                using (var xmlReader = XmlReader.Create(xsltStream))
                using (var writer = new XmlTextWriter(htmlFilePath, null))
                {
                    var transform = new XslCompiledTransform();
                    transform.Load(xmlReader);
                    transform.Transform(_xmlDocument, writer);
                }
            }
            return htmlFilePath;
        }

        private XmlElement GetProjectElement(string projectName)
        {
            return _projectElements.GetOrAdd(projectName, name =>
            {
                var projectElement = _xmlDocument.CreateElement(ProjectString);
                projectElement.SetAttribute("Name", projectName);
                projectElement.AppendChild(_xmlDocument.CreateElement(IssuesString));
                projectElement.AppendChild(_xmlDocument.CreateElement(IncludedPackagesString));
                projectElement.AppendChild(_xmlDocument.CreateElement(ExcludedPackagesString));
                _projectsElement.AppendChild(projectElement);
                return projectElement;
            });
        }

        internal enum ErrorLevel
        {
            Information,
            Warning,
            Error
        }
    }
}
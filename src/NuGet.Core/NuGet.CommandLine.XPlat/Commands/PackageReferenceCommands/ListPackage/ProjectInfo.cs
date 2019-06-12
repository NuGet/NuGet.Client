using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.ProjectModel;

namespace NuGet.CommandLine.XPlat
{
    internal class ProjectInfo
    {
        public ProjectInfo(string projectName, string projectPath, ProjectStyle projectStyle)
        {
            ProjectName = projectName;
            ProjectPath = projectPath;
            ProjectStyle = projectStyle;
            _targetFrameworkInfos = new List<TargetFrameworkInfo>();
        }

        public string ProjectName { get; }
        public string ProjectPath { get; }
        public ProjectStyle ProjectStyle { get; }
        public IEnumerable<TargetFrameworkInfo> TargetFrameworkInfos
        {
            get
            {
                return (IEnumerable<TargetFrameworkInfo>)_targetFrameworkInfos;
            }
        }

        private List<TargetFrameworkInfo> _targetFrameworkInfos;

        public void AddTargetFrameworkInfo(TargetFrameworkInfo targetFrameworkInfo)
        {
            _targetFrameworkInfos.Add(targetFrameworkInfo);
        }

        internal void AddTargetFrameworkInfos(IEnumerable<TargetFrameworkInfo> targetFrameworkInfos)
        {
            _targetFrameworkInfos.AddRange(targetFrameworkInfos);
        }
    }
}
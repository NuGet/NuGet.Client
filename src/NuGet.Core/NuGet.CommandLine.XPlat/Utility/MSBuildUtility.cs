using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using NuGet.Packaging.Core;

namespace NuGet.CommandLine.XPlat
{
    internal class MSBuildUtility
    {
        private static string PACKAGE_REFERENCE_TYPE_TAG = "PackageReference";
        private static string VERSION_TAG = "Version";

        public static Project GetProject(string projectCSProjPath)
        {
            var projectRootElement = TryOpenProjectRootElement(projectCSProjPath);
            if (projectCSProjPath == null)
            {
                throw new Exception(string.Format(CultureInfo.CurrentCulture, Strings.Error_MSBuildUnableToOpenProject));
            }
            return new Project(projectRootElement);
        }

        public static void AddPackageReferenceAllTFMs(Project project, PackageIdentity packageIdentity)
        {
            var itemGroups = GetItemGroups(project, condition: string.Empty);

            // Add packageReference only if it does not exist for any target framework
            if (!PackageReferenceExists(itemGroups, packageIdentity))
            {
                var itemGroup = GetItemGroup(project, itemGroups, PACKAGE_REFERENCE_TYPE_TAG);
                AddPackageReferenceIntoItemGroup(itemGroup, packageIdentity);
            }
        }

        private static IEnumerable<ProjectItemGroupElement> GetItemGroups(Project project, string condition = null)
        {
            IEnumerable<ProjectItemGroupElement> itemGroupsPerCondition;

            if (condition == null)
            {
                itemGroupsPerCondition = project
                    .Items
                    .Select(item => item.Xml.Parent as ProjectItemGroupElement)
                    .Distinct();
            }
            else
            {
                itemGroupsPerCondition = project
                    .Items
                    .Select(item => item.Xml.Parent as ProjectItemGroupElement)
                    .Where(itemGroupElement => itemGroupElement.Condition.Equals(condition, StringComparison.OrdinalIgnoreCase))
                    .Distinct();
            }

            // By now itemGroupsPerCondition will contain all the item groups that match the condition.
            // itemGroupsPerCondition could be null here.

            return itemGroupsPerCondition;
        }

        private static ProjectItemGroupElement GetItemGroup(Project project, IEnumerable<ProjectItemGroupElement> itemGroups, string itemType, string condition = null)
        {
            var itemGroup = itemGroups?
                .Where(itemGroupElement => itemGroupElement.Items.Any(item => item.ItemType == itemType))?
                .FirstOrDefault();

            // itemGroup will contain an item group that has a package reference tag and meets the condition.
            // itemGroup could be null here.

            if (itemGroup == null)
            {
                // This means that either no item groups exist that match the condition
                // or they do not have a package reference tag

                itemGroup = project.Xml.AddItemGroup();
                itemGroup.Condition = condition;
            }

            return itemGroup;
        }

        public static void AddPackageReferenceIntoItemGroup(ProjectItemGroupElement itemGroup, PackageIdentity packageIdentity)
        {
            var packageMetadata = new Dictionary<string, string> { { VERSION_TAG, packageIdentity.Version.ToString() } };

            // Currently metadata is added as a child. As opposed to an attribute
            // Due to https://github.com/Microsoft/msbuild/issues/1393

            itemGroup.AddItem(PACKAGE_REFERENCE_TYPE_TAG, packageIdentity.Id, packageMetadata);
            itemGroup.ContainingProject.Save();
        }

        private static bool PackageReferenceExists(IEnumerable<ProjectItemGroupElement> itemGroups, PackageIdentity packageIdentity)
        {
            if (itemGroups == null)
            {
                return false;
            }
            else
            {
                return itemGroups.Any(itemGroup => itemGroup.Items.Any(item => item.ItemType.Equals(PACKAGE_REFERENCE_TYPE_TAG)
                                                                             && item.Include.Equals(packageIdentity.Id)));
            }
        }

        // There is ProjectRootElement.TryOpen but it does not work as expected
        // I.e. it returns null for some valid projects
        private static ProjectRootElement TryOpenProjectRootElement(string filename)
        {
            try
            {
                return ProjectRootElement.Open(filename);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return null;
            }
        }
    }
}
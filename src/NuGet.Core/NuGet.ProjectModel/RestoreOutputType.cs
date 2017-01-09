namespace NuGet.ProjectModel
{
    public enum ProjectStyle : ushort
    {
        /// <summary>
        /// Unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// UAP style, project.lock.json is generated next to project.json
        /// </summary>
        ProjectJson = 1,

        /// <summary>
        /// MSBuild style, project.assets.json is generated in the RestoreOutputPath folder
        /// </summary>
        PackageReference = 2,

        /// <summary>
        /// Tool
        /// </summary>
        DotnetCliTool = 3,

        /// <summary>
        /// Non-MSBuild project with no project dependencies.
        /// </summary>
        Standalone = 4,

        /// <summary>
        /// Packages.config project
        /// </summary>
        PackagesConfig = 5
    }
}
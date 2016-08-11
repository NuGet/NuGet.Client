namespace NuGet.Commands
{
    public enum RestoreOutputType : ushort
    {
        /// <summary>
        /// Unknown
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// UAP style, project.lock.json is generated next to project.json
        /// </summary>
        UAP = 1,

        /// <summary>
        /// MSBuild style, project.assets.json is generated in the RestoreOutputPath folder
        /// </summary>
        NETCore = 2
    }
}

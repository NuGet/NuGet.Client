namespace NuGetClientTestContracts
{
    /// <summary>
    /// Derive from this interface for TestContracts that execute under devenv.exe
    /// </summary>
    public interface IPackageManageUIHostTestContract
    {
        int GetPidForRemoteProject(string projectGuid);
        void SetSharedAssetProjectActiveContext(string projectGuid, string documentPath);

    }
}

namespace NuGetConsole
{
    internal interface IHostInitializer
    {
        void Start();

        void SetDefaultRunspace();
    }
}

using Microsoft.Win32;

namespace NuGet.VisualStudio
{
    public interface IRegistryKey
    {
        IRegistryKey OpenSubKey(string name);
        object GetValue(string name);
        void Close();
    }
}

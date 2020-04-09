using System.Collections.Generic;

namespace NuGet.Packaging.Rules
{
    internal interface IValidationPackage
    {
        IEnumerable<string> GetFiles();
        IValidationNuspec NuspecReader { get; }
    }

    internal interface IValidationNuspec
    {
        string GetId();
    }

    internal class ValidationPackageAdapter : IValidationPackage
    {
        private readonly PackageArchiveReader _par;

        public ValidationPackageAdapter(PackageArchiveReader par)
        {
            _par = par;
        }

        public IValidationNuspec NuspecReader => new ValidationNuspecAdapter(_par.NuspecReader);

        public IEnumerable<string> GetFiles()
        {
            return _par.GetFiles();
        }
    }

    internal class ValidationNuspecAdapter : IValidationNuspec
    {
        private readonly NuspecReader _nr;

        public ValidationNuspecAdapter(NuspecReader nr)
        {
            _nr = nr;
        }

        public string GetId()
        {
            return _nr.GetId();
        }
    }
}

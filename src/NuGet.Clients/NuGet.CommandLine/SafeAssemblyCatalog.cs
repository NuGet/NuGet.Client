using System;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using System.Reflection;
using System.Text;
using NuGet.Common;

namespace NuGet.CommandLine
{
    internal class SafeAssemblyCatalog : ComposablePartCatalog
    {
        private ComposablePartCatalog _innerCatalog;

        private readonly string _assemblyPath;
        private readonly IConsole _console;

        public SafeAssemblyCatalog(string assemblyPath, IConsole console)
            : base()
        {
            _assemblyPath = assemblyPath;
            _console = console;
        }

        public override IQueryable<ComposablePartDefinition> Parts
        {
            get
            {
                IQueryable<ComposablePartDefinition> parts = null;
                if (_innerCatalog == null)
                {
                    ComposablePartCatalog tempCatalog = null;
                    try
                    {
                        tempCatalog = new AssemblyCatalog(_assemblyPath);
                        parts = tempCatalog.Parts;

                        var assembly = Assembly.LoadFile(_assemblyPath);
                        assembly.GetTypes();
                    }
                    catch (Exception ex)
                    {
                        // dispose old tempCatalog
                        if (tempCatalog != null)
                        {
                            tempCatalog.Dispose();
                            tempCatalog = null;
                        }

                        try
                        {
                            tempCatalog = new EmptyCatalog();
                            HandleException(ex, _assemblyPath);
                            parts = tempCatalog.Parts;
                        }
                        catch
                        {
                            if (tempCatalog != null)
                            {
                                tempCatalog.Dispose();
                                throw;
                            }
                        }
                    }

                    _innerCatalog = tempCatalog;
                }
                else
                {
                    parts = _innerCatalog.Parts;
                }

                return parts;
            }
        }

        private void HandleException(Exception ex, string assemblyPath)
        {
            var rex = ex as ReflectionTypeLoadException;

            if (rex == null)
            {
                throw ex;
            }

            var resource =
                LocalizedResourceManager.GetString(nameof(NuGetResources.FailedToLoadExtensionDuringMefComposition));

            var perAssemblyError = string.Empty;

            if (rex?.LoaderExceptions.Length > 0)
            {
                var builder = new StringBuilder();

                builder.AppendLine(string.Empty);

                var errors = rex.LoaderExceptions.Select(e => e.Message).Distinct(StringComparer.Ordinal);

                foreach (var error in errors)
                {
                    builder.AppendLine(error);
                }

                perAssemblyError = builder.ToString();
            }

            var warning = string.Format(resource, _assemblyPath, perAssemblyError);

            _console.WriteWarning(warning);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                using (_innerCatalog)
                {
                }

                _innerCatalog = null;
            }

            base.Dispose(disposing);
        }

        private class EmptyCatalog : ComposablePartCatalog
        {
            private readonly ComposablePartDefinition[] _empty = new ComposablePartDefinition[0];

            public override IQueryable<ComposablePartDefinition> Parts
            {
                get
                {
                    return Queryable.AsQueryable(_empty);
                }
            }
        }
    }
}

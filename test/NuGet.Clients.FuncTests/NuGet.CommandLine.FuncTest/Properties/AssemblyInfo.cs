using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("NuGet.CommandLine.FuncTest")]
[assembly: AssemblyDescription("")]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("6a972c6a-5a5d-42e9-a2f4-038918c80e5d")]

// Exercises the new metadata-driven manifest authoring
[assembly: AssemblyMetadata("owner", "Outercurve")]

// XUnit runner configuration: Disable parallel tests
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true, MaxParallelThreads = 1)]
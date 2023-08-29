﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NuGet.SolutionRestoreManager {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("NuGet.SolutionRestoreManager.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Argument cannot be null or empty.
        /// </summary>
        internal static string Argument_Cannot_Be_Null_Or_Empty {
            get {
                return ResourceManager.GetString("Argument_Cannot_Be_Null_Or_Empty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Package &apos;{0}&apos; does not have an exact version like &apos;[1.0.0]&apos;. Only exact versions are allowed with PackageDownload..
        /// </summary>
        internal static string Error_PackageDownload_NoVersion {
            get {
                return ResourceManager.GetString("Error_PackageDownload_NoVersion", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Package &apos;{1} {0}&apos; does not have an exact version like &apos;[1.0.0]&apos;. Only exact versions are allowed with PackageDownload..
        /// </summary>
        internal static string Error_PackageDownload_OnlyExactVersionsAreAllowed {
            get {
                return ResourceManager.GetString("Error_PackageDownload_OnlyExactVersionsAreAllowed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Error occurred while restoring NuGet packages: {0}.
        /// </summary>
        internal static string ErrorOccurredRestoringPackages {
            get {
                return ResourceManager.GetString("ErrorOccurredRestoringPackages", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to All packages are already installed and there is nothing to restore..
        /// </summary>
        internal static string NothingToRestore {
            get {
                return ResourceManager.GetString("NothingToRestore", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to read project information for &apos;{0}&apos;: {1}.
        /// </summary>
        internal static string NU1105 {
            get {
                return ResourceManager.GetString("NU1105", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to ========== Finished ==========.
        /// </summary>
        internal static string Operation_Finished {
            get {
                return ResourceManager.GetString("Operation_Finished", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Time Elapsed: {0}.
        /// </summary>
        internal static string Operation_TotalTime {
            get {
                return ResourceManager.GetString("Operation_TotalTime", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to One or more NuGet packages need to be restored but couldn&apos;t be because consent has not been granted. To give consent, open the Visual Studio Options dialog, click on the NuGet Package Manager node and check &apos;Allow NuGet to download missing packages during build.&apos; You can also give consent by setting the environment variable &apos;EnableNuGetPackageRestore&apos; to &apos;true&apos;.
        ///
        ///Missing packages: {0}.
        /// </summary>
        internal static string PackageNotRestoredBecauseOfNoConsent {
            get {
                return ResourceManager.GetString("PackageNotRestoredBecauseOfNoConsent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to NuGet restore is currently disabled. To enable it, open the Visual Studio Options dialog, click on the NuGet Package Manager node and check &apos;Allow NuGet to download missing packages during build.&apos; You can also enable it by setting the environment variable &apos;EnableNuGetPackageRestore&apos; to &apos;true&apos;..
        /// </summary>
        internal static string PackageRefNotRestoredBecauseOfNoConsent {
            get {
                return ResourceManager.GetString("PackageRefNotRestoredBecauseOfNoConsent", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to NuGet package restore canceled..
        /// </summary>
        internal static string PackageRestoreCanceled {
            get {
                return ResourceManager.GetString("PackageRestoreCanceled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to NuGet Package restore failed for project {0}: {1}. Please see Error List window for detailed warnings and errors..
        /// </summary>
        internal static string PackageRestoreFailedForProject {
            get {
                return ResourceManager.GetString("PackageRestoreFailedForProject", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to NuGet package restore finished..
        /// </summary>
        internal static string PackageRestoreFinished {
            get {
                return ResourceManager.GetString("PackageRestoreFinished", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to NuGet Package restore finished for project &apos;{0}&apos;..
        /// </summary>
        internal static string PackageRestoreFinishedForProject {
            get {
                return ResourceManager.GetString("PackageRestoreFinishedForProject", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to NuGet package restore failed. Please see Error List window for detailed warnings and errors..
        /// </summary>
        internal static string PackageRestoreFinishedWithError {
            get {
                return ResourceManager.GetString("PackageRestoreFinishedWithError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Restoring NuGet packages...
        ///To prevent NuGet from restoring packages during build, open the Visual Studio Options dialog, click on the NuGet Package Manager node and uncheck &apos;Allow NuGet to download missing packages during build.&apos;.
        /// </summary>
        internal static string PackageRestoreOptOutMessage {
            get {
                return ResourceManager.GetString("PackageRestoreOptOutMessage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;globalPackagesFolder&apos; from nuget.config file or the environment variable is &apos;{0}&apos;, a relative path and the solution is not saved. Please save your solution or configure a &apos;globalPackagesFolder&apos; which is a full path..
        /// </summary>
        internal static string RelativeGlobalPackagesFolder {
            get {
                return ResourceManager.GetString("RelativeGlobalPackagesFolder", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Restored NuGet package {0}..
        /// </summary>
        internal static string RestoredPackage {
            get {
                return ResourceManager.GetString("RestoredPackage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Restoring NuGet packages....
        /// </summary>
        internal static string RestoringPackages {
            get {
                return ResourceManager.GetString("RestoringPackages", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Solution is not saved. Please save your solution before managing NuGet packages..
        /// </summary>
        internal static string SolutionIsNotSaved {
            get {
                return ResourceManager.GetString("SolutionIsNotSaved", resourceCulture);
            }
        }
    }
}

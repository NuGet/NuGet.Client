﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NuGet.Packaging {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Strings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Strings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("NuGet.Packaging.Strings", typeof(Strings).GetTypeInfo().Assembly);
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
        ///   Looks up a localized string similar to Unsupported targetFramework value &apos;{0}&apos;..
        /// </summary>
        internal static string Error_InvalidTargetFramework {
            get {
                return ResourceManager.GetString("Error_InvalidTargetFramework", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to There are duplicate packages: {0}.
        /// </summary>
        internal static string ErrorDuplicatePackages {
            get {
                return ResourceManager.GetString("ErrorDuplicatePackages", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid allowedVersions for package id &apos;{0}&apos;: &apos;{1}&apos;.
        /// </summary>
        internal static string ErrorInvalidAllowedVersions {
            get {
                return ResourceManager.GetString("ErrorInvalidAllowedVersions", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid minClientVersion: &apos;{0}&apos;.
        /// </summary>
        internal static string ErrorInvalidMinClientVersion {
            get {
                return ResourceManager.GetString("ErrorInvalidMinClientVersion", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid package version for package id &apos;{0}&apos;: &apos;{1}&apos;.
        /// </summary>
        internal static string ErrorInvalidPackageVersion {
            get {
                return ResourceManager.GetString("ErrorInvalidPackageVersion", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid package version for a dependency with id &apos;{0}&apos; in package &apos;{1}&apos;: &apos;{2}&apos;.
        /// </summary>
        internal static string ErrorInvalidPackageVersionForDependency {
            get {
                return ResourceManager.GetString("ErrorInvalidPackageVersionForDependency", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Manifest file not found at &apos;{0}&apos;.
        /// </summary>
        internal static string ErrorManifestFileNotFound {
            get {
                return ResourceManager.GetString("ErrorManifestFileNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Null or empty package id.
        /// </summary>
        internal static string ErrorNullOrEmptyPackageId {
            get {
                return ResourceManager.GetString("ErrorNullOrEmptyPackageId", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Package is not signed..
        /// </summary>
        internal static string ErrorPackageNotSigned {
            get {
                return ResourceManager.GetString("ErrorPackageNotSigned", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Package contents might have been tampered..
        /// </summary>
        internal static string ErrorPackageTampered {
            get {
                return ResourceManager.GetString("ErrorPackageTampered", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Signing certificate chains to an untrusted root..
        /// </summary>
        internal static string ErrorSigningCertUntrustedRoot {
            get {
                return ResourceManager.GetString("ErrorSigningCertUntrustedRoot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to delete temporary file &apos;{0}&apos;. Error: &apos;{1}&apos;..
        /// </summary>
        internal static string ErrorUnableToDeleteFile {
            get {
                return ResourceManager.GetString("ErrorUnableToDeleteFile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to update file time for {0}: {1}.
        /// </summary>
        internal static string FailedFileTime {
            get {
                return ResourceManager.GetString("FailedFileTime", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fail to load packages.config as XML file. Please check it. .
        /// </summary>
        internal static string FailToLoadPackagesConfig {
            get {
                return ResourceManager.GetString("FailToLoadPackagesConfig", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to write packages.config as XML file &apos;{0}&apos;. Error: &apos;{1}&apos;..
        /// </summary>
        internal static string FailToWritePackagesConfig {
            get {
                return ResourceManager.GetString("FailToWritePackagesConfig", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to find fallback package folder &apos;{0}&apos;..
        /// </summary>
        internal static string FallbackFolderNotFound {
            get {
                return ResourceManager.GetString("FallbackFolderNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {0} This validation error occurred in a &apos;{1}&apos; element..
        /// </summary>
        internal static string InvalidNuspecElement {
            get {
                return ResourceManager.GetString("InvalidNuspecElement", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The nuspec contains an invalid entry &apos;{0}&apos; in package &apos;{1}&apos; ..
        /// </summary>
        internal static string InvalidNuspecEntry {
            get {
                return ResourceManager.GetString("InvalidNuspecEntry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The framework in the folder name of &apos;{0}&apos; in package &apos;{1}&apos; could not be parsed..
        /// </summary>
        internal static string InvalidPackageFrameworkFolderName {
            get {
                return ResourceManager.GetString("InvalidPackageFrameworkFolderName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Package {0} signature is invalid..
        /// </summary>
        internal static string InvalidPackageSignature {
            get {
                return ResourceManager.GetString("InvalidPackageSignature", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Installing {0} {1}..
        /// </summary>
        internal static string Log_InstallingPackage {
            get {
                return ResourceManager.GetString("Log_InstallingPackage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to MinClientVersion already exists in packages.config.
        /// </summary>
        internal static string MinClientVersionAlreadyExist {
            get {
                return ResourceManager.GetString("MinClientVersionAlreadyExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Nuspec file does not exist in package..
        /// </summary>
        internal static string MissingNuspec {
            get {
                return ResourceManager.GetString("MissingNuspec", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Package contains multiple nuspec files..
        /// </summary>
        internal static string MultipleNuspecFiles {
            get {
                return ResourceManager.GetString("MultipleNuspecFiles", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &apos;{0}&apos; must contain an absolute path &apos;{1}&apos;..
        /// </summary>
        internal static string MustContainAbsolutePath {
            get {
                return ResourceManager.GetString("MustContainAbsolutePath", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Package entry already exists in packages.config. Id: {0}.
        /// </summary>
        internal static string PackageEntryAlreadyExist {
            get {
                return ResourceManager.GetString("PackageEntryAlreadyExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Package entry does not exists in packages.config. Id: {0}, Version: {1}.
        /// </summary>
        internal static string PackageEntryNotExist {
            get {
                return ResourceManager.GetString("PackageEntryNotExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The &apos;{0}&apos; package requires NuGet client version &apos;{1}&apos; or above, but the current NuGet version is &apos;{2}&apos;. To upgrade NuGet, please go to http://docs.nuget.org/consume/installing-nuget.
        /// </summary>
        internal static string PackageMinVersionNotSatisfied {
            get {
                return ResourceManager.GetString("PackageMinVersionNotSatisfied", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Packages node does not exists in packages.config at {0}..
        /// </summary>
        internal static string PackagesNodeNotExist {
            get {
                return ResourceManager.GetString("PackagesNodeNotExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Package stream should be seekable.
        /// </summary>
        internal static string PackageStreamShouldBeSeekable {
            get {
                return ResourceManager.GetString("PackageStreamShouldBeSeekable", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The package being signed already contains a signature. Please remove the existing signature before adding a new signature..
        /// </summary>
        internal static string SignedPackagePackageAlreadySigned {
            get {
                return ResourceManager.GetString("SignedPackagePackageAlreadySigned", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The package is not signed. Unable to remove signature from an unsigned package..
        /// </summary>
        internal static string SignedPackagePackageNotSigned {
            get {
                return ResourceManager.GetString("SignedPackagePackageNotSigned", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The package was not opened correctly to perform Signature operations. Please use a Stream based constructor to have access to Signature attributes of the package..
        /// </summary>
        internal static string SignedPackageUnableToAccessSignature {
            get {
                return ResourceManager.GetString("SignedPackageUnableToAccessSignature", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to String argument &apos;{0}&apos; cannot be null or empty.
        /// </summary>
        internal static string StringCannotBeNullOrEmpty {
            get {
                return ResourceManager.GetString("StringCannotBeNullOrEmpty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The timestamp service&apos;s certificate chain could not be built for the following certificate - 
        ///{0}.
        /// </summary>
        internal static string TimestampCertificateChainBuildFailure {
            get {
                return ResourceManager.GetString("TimestampCertificateChainBuildFailure", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Author certificate was not valid when it was timestamped..
        /// </summary>
        internal static string TimestampFailureAuthorCertNotValid {
            get {
                return ResourceManager.GetString("TimestampFailureAuthorCertNotValid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timestamp service&apos;s certificate does not contain a valid Enhanced Key Usage for timestamping..
        /// </summary>
        internal static string TimestampFailureCertInvalidEku {
            get {
                return ResourceManager.GetString("TimestampFailureCertInvalidEku", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timestamp signature contains invalid content type..
        /// </summary>
        internal static string TimestampFailureInvalidContentType {
            get {
                return ResourceManager.GetString("TimestampFailureInvalidContentType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timestamp response contains invalid signature value hash..
        /// </summary>
        internal static string TimestampFailureInvalidHash {
            get {
                return ResourceManager.GetString("TimestampFailureInvalidHash", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timestamp response does not contain an acceptable hash algorithm..
        /// </summary>
        internal static string TimestampFailureInvalidHashAlgorithmOid {
            get {
                return ResourceManager.GetString("TimestampFailureInvalidHashAlgorithmOid", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The timestamper url &apos;{0}&apos; has an invalid uri scheme. The supported schemes are &apos;{1}&apos; and &apos;{2}&apos;..
        /// </summary>
        internal static string TimestampFailureInvalidHttpScheme {
            get {
                return ResourceManager.GetString("TimestampFailureInvalidHttpScheme", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timestamp response does not contain a matching response..
        /// </summary>
        internal static string TimestampFailureNonceMismatch {
            get {
                return ResourceManager.GetString("TimestampFailureNonceMismatch", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timestamp service&apos;s response does not meet the NuGet package signature specification: &apos;{0}&apos;..
        /// </summary>
        internal static string TimestampResponseExceptionGeneral {
            get {
                return ResourceManager.GetString("TimestampResponseExceptionGeneral", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to An error occurred while updating packages.config. The file was closed before the entry could be added..
        /// </summary>
        internal static string UnableToAddEntry {
            get {
                return ResourceManager.GetString("UnableToAddEntry", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unable to parse the current NuGet client version..
        /// </summary>
        internal static string UnableToParseClientVersion {
            get {
                return ResourceManager.GetString("UnableToParseClientVersion", resourceCulture);
            }
        }
    }
}

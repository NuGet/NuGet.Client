﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NuGet.Configuration {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///    A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    public class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        internal Resources() {
        }
        
        /// <summary>
        ///    Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("NuGet.Configuration.Resources", typeof(Resources).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///    Overrides the current thread's CurrentUICulture property for all
        ///    resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Value cannot be null or empty string..
        /// </summary>
        public static string Argument_Cannot_Be_Null_Or_Empty {
            get {
                return ResourceManager.GetString("Argument_Cannot_Be_Null_Or_Empty", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to There are no writable config files..
        /// </summary>
        public static string Error_NoWritableConfig {
            get {
                return ResourceManager.GetString("Error_NoWritableConfig", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to File &apos;{0}&apos; does not exist..
        /// </summary>
        public static string FileDoesNotExist {
            get {
                return ResourceManager.GetString("FileDoesNotExist", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to &quot;{0}&quot; cannot be called on a NullSettings. This may be caused on account of insufficient permissions to read or write to &quot;%AppData%\NuGet\NuGet.config&quot;..
        /// </summary>
        public static string InvalidNullSettingsOperation {
            get {
                return ResourceManager.GetString("InvalidNullSettingsOperation", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to &apos;{0}&apos; must contain an absolute path &apos;{1}&apos;..
        /// </summary>
        public static string MustContainAbsolutePath {
            get {
                return ResourceManager.GetString("MustContainAbsolutePath", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to The package source does not belong to the collection of available sources..
        /// </summary>
        public static string PackageSource_Invalid {
            get {
                return ResourceManager.GetString("PackageSource_Invalid", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Environment variable &apos;{0}&apos; must contain an absolute path, the full path of &apos;{1}&apos; cannot be determined..
        /// </summary>
        public static string RelativeEnvVarPath {
            get {
                return ResourceManager.GetString("RelativeEnvVarPath", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Parameter &apos;fileName&apos; to Settings must be just a fileName and not a path.
        /// </summary>
        public static string Settings_FileName_Cannot_Be_A_Path {
            get {
                return ResourceManager.GetString("Settings_FileName_Cannot_Be_A_Path", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to NuGet.Config is malformed. Path: &apos;{0}&apos;..
        /// </summary>
        public static string ShowError_ConfigInvalidOperation {
            get {
                return ResourceManager.GetString("ShowError_ConfigInvalidOperation", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to NuGet.Config is not valid XML. Path: &apos;{0}&apos;..
        /// </summary>
        public static string ShowError_ConfigInvalidXml {
            get {
                return ResourceManager.GetString("ShowError_ConfigInvalidXml", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to NuGet.Config does not contain the expected root element: &apos;configuration&apos;. Path: &apos;{0}&apos;..
        /// </summary>
        public static string ShowError_ConfigRootInvalid {
            get {
                return ResourceManager.GetString("ShowError_ConfigRootInvalid", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Failed to read NuGet.Config due to unauthorized access. Path: &apos;{0}&apos;..
        /// </summary>
        public static string ShowError_ConfigUnauthorizedAccess {
            get {
                return ResourceManager.GetString("ShowError_ConfigUnauthorizedAccess", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Unexpected failure reading NuGet.Config. Path: &apos;{0}&apos;..
        /// </summary>
        public static string Unknown_Config_Exception {
            get {
                return ResourceManager.GetString("Unknown_Config_Exception", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Password decryption is not supported on .NET Core for this platform. The following feed uses an encrypted password: &apos;{0}&apos;. You can use a clear text password as a workaround.
        /// </summary>
        public static string UnsupportedDecryptPassword {
            get {
                return ResourceManager.GetString("UnsupportedDecryptPassword", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Password encryption is not supported on .NET Core for this platform. The following feed try to use an encrypted password: &apos;{0}&apos;. You can use a clear text password as a workaround.
        /// </summary>
        public static string UnsupportedEncryptPassword {
            get {
                return ResourceManager.GetString("UnsupportedEncryptPassword", resourceCulture);
            }
        }
        
        /// <summary>
        ///    Looks up a localized string similar to Unable to parse config file &apos;{0}&apos;..
        /// </summary>
        public static string UserSettings_UnableToParseConfigFile {
            get {
                return ResourceManager.GetString("UserSettings_UnableToParseConfigFile", resourceCulture);
            }
        }
    }
}

﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NuGet.Credentials {
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("NuGet.Credentials.Resources", typeof(Resources).GetTypeInfo().Assembly);
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
        ///   Looks up a localized string similar to Argument must not be null or empty..
        /// </summary>
        internal static string Error_Argument_May_Not_Be_Null_Or_Empty {
            get {
                return ResourceManager.GetString("Error_Argument_May_Not_Be_Null_Or_Empty", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Credential plugin {0} handles this request, but is unable to provide credentials. {1}.
        /// </summary>
        internal static string PluginException_Abort_Format {
            get {
                return ResourceManager.GetString("PluginException_Abort_Format", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Credential plugin {0} encountered exception {1}..
        /// </summary>
        internal static string PluginException_Exception_Format {
            get {
                return ResourceManager.GetString("PluginException_Exception_Format", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Credential plugin {0} returned {1}, but the payload was not valid (username = {2}, password = {3}, authTypes = {4}, message = {5})..
        /// </summary>
        internal static string PluginException_InvalidResponse_Format {
            get {
                return ResourceManager.GetString("PluginException_InvalidResponse_Format", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Credential plugin {0} failed to start..
        /// </summary>
        internal static string PluginException_NotStarted_Format {
            get {
                return ResourceManager.GetString("PluginException_NotStarted_Format", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Credential plugin {0} not found at any of the following locations {1}..
        /// </summary>
        internal static string PluginException_PathNotFound_Format {
            get {
                return ResourceManager.GetString("PluginException_PathNotFound_Format", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Credential plugin {0} timed out after {1} seconds..
        /// </summary>
        internal static string PluginException_Timeout_Format {
            get {
                return ResourceManager.GetString("PluginException_Timeout_Format", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Credential plugin {0} exited with unexpected error {1}..
        /// </summary>
        internal static string PluginException_UnexpectedStatus_Format {
            get {
                return ResourceManager.GetString("PluginException_UnexpectedStatus_Format", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Credential plugin {0} returned {1} with an unreadable payload..
        /// </summary>
        internal static string PluginException_UnreadableResponse_Format {
            get {
                return ResourceManager.GetString("PluginException_UnreadableResponse_Format", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not create credential response object because the response was invalid..
        /// </summary>
        internal static string ProviderException_InvalidCredentialResponse {
            get {
                return ResourceManager.GetString("ProviderException_InvalidCredentialResponse", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Credential provider gave an invalid response..
        /// </summary>
        internal static string ProviderException_MalformedResponse {
            get {
                return ResourceManager.GetString("ProviderException_MalformedResponse", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Using {0} as a credential provider plugin..
        /// </summary>
        internal static string SecurePluginNotice_UsingPluginAsProvider {
            get {
                return ResourceManager.GetString("SecurePluginNotice_UsingPluginAsProvider", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The plugin credential provider could not acquire credentials. Authentication may require manual action. Consider re-running the command with {0}.
        /// </summary>
        internal static string SecurePluginWarning_UseInteractiveOption {
            get {
                return ResourceManager.GetString("SecurePluginWarning_UseInteractiveOption", resourceCulture);
            }
        }
    }
}

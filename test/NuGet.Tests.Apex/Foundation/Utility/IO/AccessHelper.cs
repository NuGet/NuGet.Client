using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Tests.Foundation.Utility.IO
{
    public static class AccessHelper
    {
        [SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly SecurityIdentifier AllApplicationPackagesSecurityIdentifier = new SecurityIdentifier("S-1-15-2-1");

        private static readonly string AclLogFileFormat = "expression{0}_Acl_Log.txt";
        private static readonly string AclLoggingEnvVar = "EXPRESSION_LOG_ACLING";

        private static IAccessService accessService = new AccessService(OSHelper.IsOSVersionOrLater(OSHelper.Win8Version));

        public static IAccessService AccessService
        {
            get
            {
                return AccessHelper.accessService;
            }
            set
            {
                if (value is AccessService)
                {
                    // Allow the person who changed the access service to put the original one back.
                    accessService = value;
                    return;
                }

                if (!AccessHelper.IsAccessServiceLocal)
                {
                    throw new ArgumentException("Cannot set the AccessService more than once.");
                }
                AccessHelper.accessService = value;
                Debug.Assert(!AccessHelper.IsAccessServiceLocal); // no need to throw, AccessService class is internal to Utility so only this assembly could make this assert fire
            }
        }

        /// <summary>
        /// Adds the given path to a list of "safe" paths that can be locally read by
        /// app container processes.  This must be configured early before app container
        /// processes are created.
        /// </summary>
        private static void AddSafeAppContainerPath(string path)
        {
            AccessService acc = AccessHelper.accessService as AccessService;
            if (acc != null)
            {
                acc.AddSafeAppContainerPath(path);
            }
        }

        public static bool IsAccessServiceLocal
        {
            get { return AccessHelper.AccessService is AccessService; }
        }

        private static bool IsFileAccessAclSet(AuthorizationRuleCollection rules, FileSystemAccessRule desiredRule)
        {
            foreach (AccessRule rule in rules)
            {
                FileSystemAccessRule fsRule = rule as FileSystemAccessRule;
                if (fsRule == null)
                {
                    continue;
                }
                else if (fsRule.AccessControlType == desiredRule.AccessControlType
                    && fsRule.FileSystemRights == desiredRule.FileSystemRights
                    && fsRule.IdentityReference == desiredRule.IdentityReference
                    && fsRule.InheritanceFlags == desiredRule.InheritanceFlags
                    && fsRule.IsInherited == desiredRule.IsInherited
                    && fsRule.PropagationFlags == desiredRule.PropagationFlags)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Makes sure that given directory is Acl'ed for ALL APPLICATION PACKAGES Read and ReadExecute.
        /// </summary>
        /// <param name="path">Path to be Acl'ed</param>
        /// <returns>true:path successfully Acl'ed, false: failed to Acl</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public static bool EnsureAclDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Debug.Fail("Path does not exist: {0}", path);
                return false;
            }

            if (!AccessHelper.IsAccessibleByAllApplicationPackages(path))
            {
                try
                {
                    AccessHelper.AclDirectoryForApplicationPackages(path, FileSystemRights.ReadAndExecute | FileSystemRights.Write);
                }
                catch (Exception e)
                {
                    Debug.Fail(string.Format("Fail calling AclDirectoryForApplicationPackages. {0}", e.Message));
                }

                if (!AccessHelper.IsAccessibleByAllApplicationPackages(path))
                {
                    Debug.Fail("Path is not properly ACL'ed {0}", path);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// ACLs a given directory for ALL APPLICATION PACKAGES Read and ReadExecute.
        /// </summary>
        public static void AclDirectoryForApplicationPackages(string path, FileSystemRights rights = FileSystemRights.ReadAndExecute | FileSystemRights.Read)
        {
            if (!OSHelper.IsOSVersionOrLater(OSHelper.Win8Version))
            {
                // ALL APPLICATION PACKAGES doesn't exist prior to Win8
                return;
            }
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            if (!PathHelper.IsDirectory(path))
            {
                return;
            }

            FileSystemAccessRule ruleToSet = new FileSystemAccessRule(
                    AllApplicationPackagesSecurityIdentifier,
                    rights,
                    InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow);

            DirectorySecurity security = System.IO.Directory.GetAccessControl(path);
            AuthorizationRuleCollection rules = security.GetAccessRules(true, false, AllApplicationPackagesSecurityIdentifier.GetType());

            if (!IsFileAccessAclSet(rules, ruleToSet))
            {
                security.AddAccessRule(ruleToSet);
                System.IO.Directory.SetAccessControl(path, security);
            }

            // Mark this directory as safe for local access in the
            // access service.
            AccessHelper.AddSafeAppContainerPath(path);
        }

        /// <summary>
        /// ACLs a given file for ALL APPLICATION PACKAGES Read.
        /// </summary>
        public static void AclFileForApplicationPackages(string path, FileSystemRights rights = FileSystemRights.Read)
        {
            if (!OSHelper.IsOSVersionOrLater(OSHelper.Win8Version))
            {
                return;	// There is no such access rule before Win8.
            }
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            if (!PathHelper.FileExists(path))
            {
                return;
            }

            FileSecurity security = System.IO.File.GetAccessControl(path);
            security.AddAccessRule(new FileSystemAccessRule(
                AllApplicationPackagesSecurityIdentifier,
                rights,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Allow));

            System.IO.File.SetAccessControl(path, security);
        }

        /// <summary>
        /// ACLs a set of paths for ALL APPLICATION PACKAGES Read.
        /// This is useful in the context when we need to Acl in elevated Context and blend in running in non-elevated mode
        /// Code uses "runas" Verb while launching icacls.exe to ensure it is launched in elevated context..
        /// </summary>
        public static void AclPathsForApplicationPackages(string[] paths)
        {
            if (!OSHelper.IsOSVersionOrLater(OSHelper.Win8Version))
            {
                return;	// There is no such access rule before Win8.
            }

            if (paths == null || paths.Length == 0)
            {
                return;
            }

            string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string cmdExePath = Path.Combine(systemPath, "cmd.exe");
            string icaclsPath = Path.Combine(systemPath, "icacls.exe");
            string applicationPackageSidAsString = AccessHelper.AllApplicationPackagesSecurityIdentifier.Value;

            Queue<string> icaclsCommands = new Queue<string>();

            // Check if we need to enable logging
            string logFileFullPath = GetAclingLogFileName();
            string redirectionSuffix = !string.IsNullOrEmpty(logFileFullPath) ? string.Format(CultureInfo.CurrentUICulture, ">> {0} 2>&1", logFileFullPath) : string.Empty;

            foreach (string path in paths)
            {
                string directoryPath = PathHelper.TrimTrailingDirectorySeparators(path);
                if (PathHelper.FileOrDirectoryExists(path))
                {
                    // Note that this command doesn't properly handle directories that have been symlinked (icacls requires the /L parameter for that).
                    icaclsCommands.Enqueue(string.Format(CultureInfo.InvariantCulture, "{0} \"{1}\" /grant \"*{2}\":(OI)(CI)(IO)(GR,GE) /grant \"*{2}\":(RX) {3}", icaclsPath, directoryPath, applicationPackageSidAsString, redirectionSuffix));
                }
            }

            List<Task> tasks = new List<Task>();
            while (icaclsCommands.Count > 0)
            {
                StringBuilder builder = new StringBuilder(icaclsCommands.Dequeue());
                while (icaclsCommands.Count > 0 && ((builder.Length + icaclsCommands.Peek().Length) < 1900))
                {
                    builder.Append(" & "); // use & instead of &&, since && will return as soon as first command fails
                    builder.Append(icaclsCommands.Dequeue());
                }

                ProcessStartInfo processStartInfo = new ProcessStartInfo()
                {
                    FileName = cmdExePath,
                    Arguments = string.Format(CultureInfo.InvariantCulture, "/q /c \"{0}\"", builder.ToString()),
                    UseShellExecute = true,
                    Verb = "runas",
                };


                Task task = StartProcess(processStartInfo);
                tasks.Add(task);
            }

            // We'll wait up to 5 minutes for the ACLing to complete before we give up and assume something went wrong.
            // Success or failure will be checked independently of this function, so we can just return when done.
            Task.WaitAll(tasks.ToArray(), 300 * 1000);
        }

        private static Task StartProcess(ProcessStartInfo processStartInfo)
        {
            TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>();

            Process process = new Process();
            process.StartInfo = processStartInfo;
            process.EnableRaisingEvents = true;
            process.Exited += (sender, e) => taskCompletionSource.SetResult(process.ExitCode);
            process.Start();

            return taskCompletionSource.Task;
        }

        public static bool IsAccessibleByAllApplicationPackages(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }
            if (!PathHelper.FileOrDirectoryExists(path))
            {
                return true;
            }

            FileSecurity security = System.IO.File.GetAccessControl(path);
            AuthorizationRuleCollection rules = security.GetAccessRules(includeExplicit: true, includeInherited: true, targetType: typeof(SecurityIdentifier));
            foreach (AuthorizationRule rule in rules)
            {
                FileSystemAccessRule fileRule = rule as FileSystemAccessRule;
                if (fileRule != null
                    && fileRule.AccessControlType == AccessControlType.Allow
                    && fileRule.IdentityReference.Equals(AllApplicationPackagesSecurityIdentifier)
                    && fileRule.FileSystemRights.HasFlag(FileSystemRights.Read | FileSystemRights.ReadAndExecute | FileSystemRights.ListDirectory))
                {
                    AccessHelper.AddSafeAppContainerPath(path);
                    return true;
                }
            }
            return false;
        }

        private static string GetAclingLogFileName()
        {
            string logFileFullPath = string.Empty;

            bool enableLogging = string.Equals(Environment.GetEnvironmentVariable(AclLoggingEnvVar), "1", StringComparison.OrdinalIgnoreCase);
            if (enableLogging)
            {
                string logFileName = string.Format(CultureInfo.CurrentUICulture, AclLogFileFormat, Process.GetCurrentProcess().Id);
                logFileFullPath = Path.Combine(Path.GetTempPath(), logFileName);
            }

            // if for some reason we don't get a valid path here, don't enable logging
            return PathHelper.IsValidPath(logFileFullPath) ? logFileFullPath : string.Empty;
        }

#if !UNITTESTDRIVER
        public static IFileChangeWatcherService GetOrCreateFileChangeWatcherService()
        {
            AccessService service = accessService as AccessService;

            if (service != null)
            {
                return service.GetOrCreateFileChangeWatcherService();
            }

            return null;
        }
#endif
    }
}

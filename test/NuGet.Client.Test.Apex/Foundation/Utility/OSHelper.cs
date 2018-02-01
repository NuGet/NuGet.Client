using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace NuGetClient.Test.Foundation.Utility
{
    public static class OSHelper
    {
        private static Lazy<bool> LazyIsRunningElevated = new Lazy<bool>(CalculateIsRunningElevated);
        private static Lazy<bool> LazyIsRunningInAppContainer = new Lazy<bool>(CalculateIsRunningInAppContainer);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes")]
        public static readonly IEnvironment DefaultEnvironmentAdapter = new EnvironmentAdapter();
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        public static readonly Version Win8Version = new Version(6, 2, 9200);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        public static readonly Version WinBlueVersion = new Version(6, 3, 0);
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        public static readonly Version Win9Version = new Version(6, 4, 0);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static bool IsOSVersionOrLater(Version version)
        {
            return DefaultEnvironmentAdapter.IsOSVersionOrLater(version);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public static bool IsRunningElevated
        {
            get
            {
                return LazyIsRunningElevated.Value;
            }
        }

        public static bool IsRunningInAppContainer
        {
            get
            {
                return LazyIsRunningInAppContainer.Value;
            }
        }

        private static bool CalculateIsRunningElevated()
        {
            IntPtr tokenInfo = GetTokenInformation(NativeMethods.TOKEN_INFORMATION_CLASS.TokenElevationType);
            if (tokenInfo == IntPtr.Zero)
            {
                return false;
            }

            bool isRunningElevated = (int)Marshal.PtrToStructure(tokenInfo, typeof(int)) == (int)NativeMethods.TokenElevationType.TokenElevationTypeFull;
            Marshal.FreeHGlobal(tokenInfo);
            return isRunningElevated;
        }

        private static bool CalculateIsRunningInAppContainer()
        {
            IntPtr tokenInfo = GetTokenInformation(NativeMethods.TOKEN_INFORMATION_CLASS.TokenIsAppContainer);
            if (tokenInfo == IntPtr.Zero)
            {
                return false;
            }

            bool isRunningInAppContainer = (bool)Marshal.PtrToStructure(tokenInfo, typeof(bool));
            Marshal.FreeHGlobal(tokenInfo);
            return isRunningInAppContainer;
        }

        private static IntPtr GetTokenInformation(NativeMethods.TOKEN_INFORMATION_CLASS tokenInformationClass)
        {
            IntPtr tokenInfo = IntPtr.Zero;
            IntPtr tokenHandle = IntPtr.Zero;

            try
            {
                Process process = Process.GetCurrentProcess();

                if (NativeMethods.OpenProcessToken(process.Handle, TokenAccessLevels.Query, ref tokenHandle))
                {
                    uint tokenInfoLength = 0;
                    // Note: This call fails with ERROR_INSUFFICIENT_BUFFER while it updates tokenInfoLength, so we check for that
                    if (!NativeMethods.GetTokenInformation(tokenHandle,
                        tokenInformationClass,
                        IntPtr.Zero,
                        tokenInfoLength,
                        out tokenInfoLength)
                        && Marshal.GetLastWin32Error() != NativeMethods.ERROR_INSUFFICIENT_BUFFER)
                    {
                        Debug.WriteLine("GetTokenInformation failed: Error {0}", Marshal.GetLastWin32Error());
                    }
                    else
                    {
                        tokenInfo = Marshal.AllocHGlobal((int)tokenInfoLength);
                        if (!NativeMethods.GetTokenInformation(tokenHandle,
                            tokenInformationClass,
                            tokenInfo,
                            tokenInfoLength,
                            out tokenInfoLength))
                        {
                            Debug.WriteLine("GetTokenInformation failed: Error {0}", Marshal.GetLastWin32Error());
                            NativeMethods.CloseHandle(tokenInfo);
                            tokenInfo = IntPtr.Zero;
                        }
                    }
                }
            }
            finally
            {
                if (tokenHandle != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(tokenHandle);
                }
            }
            return tokenInfo;
        }

        private static class NativeMethods
        {
            internal const int ERROR_INSUFFICIENT_BUFFER = 122;

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool OpenProcessToken([In] IntPtr processToken, [In] TokenAccessLevels DesiredAccess, [In, Out] ref IntPtr TokenHandle);

            [DllImport("kernel32", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr hObject);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetTokenInformation(
                IntPtr TokenHandle,
                TOKEN_INFORMATION_CLASS TokenInformationClass,
                IntPtr TokenInformation,
                uint TokenInformationLength,
                out uint ReturnLength);

            internal enum TOKEN_INFORMATION_CLASS
            {
                TokenUser = 1,
                TokenGroups,
                TokenPrivileges,
                TokenOwner,
                TokenPrimaryGroup,
                TokenDefaultDacl,
                TokenSource,
                TokenType,
                TokenImpersonationLevel,
                TokenStatistics,
                TokenRestrictedSids,
                TokenSessionId,
                TokenGroupsAndPrivileges,
                TokenSessionReference,
                TokenSandBoxInert,
                TokenAuditPolicy,
                TokenOrigin,
                TokenElevationType,
                TokenLinkedToken,
                TokenElevation,
                TokenHasRestrictions,
                TokenAccessInformation,
                TokenVirtualizationAllowed,
                TokenVirtualizationEnabled,
                TokenIntegrityLevel,
                TokenUIAccess,
                TokenMandatoryPolicy,
                TokenLogonSid,
                TokenIsAppContainer,
                TokenCapabilities,
                TokenAppContainerSid,
                TokenAppContainerNumber,
                TokenUserClaimAttributes,
                TokenDeviceClaimAttributes,
                TokenRestrictedUserClaimAttributes,
                TokenRestrictedDeviceClaimAttributes,
                TokenDeviceGroups,
                TokenRestrictedDeviceGroups,
                MaxTokenInfoClass  // MaxTokenInfoClass should always be the last enum
            }

            public enum TokenElevationType
            {
                TokenElevationTypeDefault = 1,
                TokenElevationTypeFull,
                TokenElevationTypeLimited
            };
        }

        /// <summary>
        /// Interface that decouples us from OS environment
        /// </summary>
        public interface IEnvironment
        {
            Version OSVersion { get; set; }

            bool IsOSVersionOrLater(Version version);
        }

        /// <summary>
        /// OS environment adapter.
        /// </summary>
        public class EnvironmentAdapter : IEnvironment
        {
            public Version OSVersion { get; set; }

            public EnvironmentAdapter()
            {
                this.OSVersion = Environment.OSVersion.Version;
            }

            public bool IsOSVersionOrLater(Version version)
            {
                return this.OSVersion >= version;
            }
        }
    }
}

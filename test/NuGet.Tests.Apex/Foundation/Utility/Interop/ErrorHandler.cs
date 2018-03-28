using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Tests.Foundation.Utility.Interop
{
    public static class ErrorHandler
    {
        // General Notes
        //
        // DWORD is UInt32 (unsigned long in C++)
        // HRESULT is an Int32 (or long in C++)

        // From WinError.h:
        private const Int32 FACILITY_WINDOWS = 8;

        private static Int32 HRESULT_CODE(Int32 hr)
        {
            // From WinError.h:
            // #define HRESULT_CODE(hr)    ((hr) & 0xFFFF)
            return hr & 0xFFFF;
        }

        private static Int32 HRESULT_FACILITY(Int32 hr)
        {
            // From WinError.h:
            // #define HRESULT_FACILITY(hr)  (((hr) >> 16) & 0x1fff)
            return (hr >> 16) & 0x1fff;
        }

        private static Int32 ConvertHResult(Int32 result)
        {
            if (HRESULT_FACILITY(result) == FACILITY_WINDOWS)
            {
                return HRESULT_CODE(result);
            }
            return result;
        }

        public static string HResultToString(Int32 result)
        {
            Int32 convertedHResult = ErrorHandler.ConvertHResult(result);
            return String.Format(
                CultureInfo.CurrentUICulture,
                "HResult {0:D} [0x{0:X}]: {1}",
                result.ToString(CultureInfo.InvariantCulture),
                ErrorHandler.FormatMessage(convertedHResult));
        }

        public static string LastErrorToString(Int32 errorNumber)
        {
            return String.Format(
                CultureInfo.CurrentUICulture,
                "Error {0}: {1}",
                errorNumber.ToString(CultureInfo.InvariantCulture),
                ErrorHandler.FormatMessage(errorNumber));
        }

        private static string FormatMessage(Int32 errorCode)
        {
            return new Win32Exception(errorCode).Message;
        }

        public static void ThrowWindowsError(Int32 errorCode, string description)
        {
            throw new Win32Exception(errorCode, String.Concat(description, " ",
                ErrorHandler.LastErrorToString(errorCode)));
        }

        public static void ThrowHResultError(Int32 result, string description)
        {
            throw new Win32Exception(ErrorHandler.ConvertHResult(result), String.Concat(description, " ",
                ErrorHandler.HResultToString(result)));
        }
    }
}

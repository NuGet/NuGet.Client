using System;
using System.Collections.Generic;
using System.Text;

namespace NuGet.Common
{
    /// <summary>
    /// These are Warning Levels used by NuGet while throwing warnings.
    /// These logically correspond to .NET spec at https://msdn.microsoft.com/en-us/library/13b90fz7(v=vs.140).aspx
    /// 
    /// We do not have a level 0 as that has no logical meaning of having a warning with level 0.
    /// 
    /// Severe - This should be used to throw warnings that just short of being an error.
    ///          Can be ignored if the project warn level is 0.
    /// 
    /// Important - Lower level than severe. 
    ///             Can be ignored if the project warn level is 1 or 0.
    /// 
    /// Minimal - Lower level than important. 
    ///           Can be ignored if the project warn level is 0, 1 or 2.
    /// 
    /// Default - Lowest level of warnings. 
    ///           Can be ignored if the project warn level is 0, 1, 2 or 3. 
    ///           Further default NuGet logging will ignore these warnings.
    /// </summary>
    public enum WarningLevel
    {
        Severe = 1,
        Important = 2,
        Minimal = 3,
        Default = 4
    }
}

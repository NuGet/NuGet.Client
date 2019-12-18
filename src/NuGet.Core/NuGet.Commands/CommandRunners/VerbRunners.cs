using System;
using System.Threading.Tasks;
using NuGet.Common;


namespace NuGet.CommandLine.XPlat
{


 
    public partial class AddSourceRunner
    {
        static public void DebugParameters(AddSourceArgs args, Func<ILogger> getLogger)
        {
            Output(args, getLogger);
        }

        static void Output(AddSourceArgs args, Func<ILogger> getLogger)
        {
            Type argsType = args.GetType();
            foreach (var propInfo in argsType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                getLogger().LogInformation(propInfo.Name + " = " + propInfo.GetValue(args));
            }

            return;
        }
    } 


    public partial class DisableSourceRunner
    {
        static public void DebugParameters(DisableSourceArgs args, Func<ILogger> getLogger)
        {
            Output(args, getLogger);
        }

        static void Output(DisableSourceArgs args, Func<ILogger> getLogger)
        {
            Type argsType = args.GetType();
            foreach (var propInfo in argsType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                getLogger().LogInformation(propInfo.Name + " = " + propInfo.GetValue(args));
            }

            return;
        }
    } 


    public partial class EnableSourceRunner
    {
        static public void DebugParameters(EnableSourceArgs args, Func<ILogger> getLogger)
        {
            Output(args, getLogger);
        }

        static void Output(EnableSourceArgs args, Func<ILogger> getLogger)
        {
            Type argsType = args.GetType();
            foreach (var propInfo in argsType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                getLogger().LogInformation(propInfo.Name + " = " + propInfo.GetValue(args));
            }

            return;
        }
    } 


    public partial class ListSourceRunner
    {
        static public void DebugParameters(ListSourceArgs args, Func<ILogger> getLogger)
        {
            Output(args, getLogger);
        }

        static void Output(ListSourceArgs args, Func<ILogger> getLogger)
        {
            Type argsType = args.GetType();
            foreach (var propInfo in argsType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                getLogger().LogInformation(propInfo.Name + " = " + propInfo.GetValue(args));
            }

            return;
        }
    } 


    public partial class RemoveSourceRunner
    {
        static public void DebugParameters(RemoveSourceArgs args, Func<ILogger> getLogger)
        {
            Output(args, getLogger);
        }

        static void Output(RemoveSourceArgs args, Func<ILogger> getLogger)
        {
            Type argsType = args.GetType();
            foreach (var propInfo in argsType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                getLogger().LogInformation(propInfo.Name + " = " + propInfo.GetValue(args));
            }

            return;
        }
    } 


    public partial class UpdateSourceRunner
    {
        static public void DebugParameters(UpdateSourceArgs args, Func<ILogger> getLogger)
        {
            Output(args, getLogger);
        }

        static void Output(UpdateSourceArgs args, Func<ILogger> getLogger)
        {
            Type argsType = args.GetType();
            foreach (var propInfo in argsType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                getLogger().LogInformation(propInfo.Name + " = " + propInfo.GetValue(args));
            }

            return;
        }
    } 


}

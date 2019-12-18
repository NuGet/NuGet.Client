using System;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;

namespace NuGet.CommandLine.XPlat
{
    public partial class AddSourceRunner
    {
        static public void Run(AddSourceArgs args, Func<ILogger> getLogger)
        {
            // TODO: remove debugging (or diagnostic?)
            Output(args, getLogger);

            if (string.Equals(args.Name, Strings.ReservedPackageNameAll))
            {
                throw new CommandException(Strings.SourcesCommandAllNameIsReserved);
            }

            // Make sure that the Source given is a valid one.
            if (!PathValidator.IsValidSource(args.Source))
            {
                throw new CommandException(Strings.SourcesCommandInvalidSource);
            }

            RunnerHelper.ValidateCredentials(args.Username, args.Password, args.ValidAuthenticationTypes);

            var settings = RunnerHelper.GetSettings(args.Configfile);
            var sourceProvider = RunnerHelper.GetSourceProvider(settings);

            // Check to see if we already have a registered source with the same name or source
            var existingSourceWithName = sourceProvider.GetPackageSourceByName(args.Name);
            if (existingSourceWithName != null)
            {
                throw new CommandException(Strings.SourcesCommandUniqueName);
            }

            var existingSourceWithSource = sourceProvider.GetPackageSourceBySource(args.Source);
            if (existingSourceWithSource != null)
            {
                throw new CommandException(Strings.SourcesCommandUniqueSource);
            }

            var newPackageSource = new Configuration.PackageSource(args.Source, args.Name);

            if (!string.IsNullOrEmpty(args.Username))
            {
                var credentials = Configuration.PackageSourceCredential.FromUserInput(
                    args.Name,
                    args.Username,
                    args.Password,
                    args.StorePasswordInClearText,
                    args.ValidAuthenticationTypes);
                newPackageSource.Credentials = credentials;
            }

            sourceProvider.AddPackageSource(newPackageSource);
            getLogger().LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandSourceAddedSuccessfully, args.Name));
        }
    }


    public partial class DisableSourceRunner
    {
        static public void Run(DisableSourceArgs args, Func<ILogger> getLogger)
        {
            var settings = RunnerHelper.GetSettings(args.Configfile);
            var sourceProvider = RunnerHelper.GetSourceProvider(settings);
            RunnerHelper.EnableOrDisableSource(sourceProvider, args.Name, enabled: false, getLogger);
        }
    }


    public partial class EnableSourceRunner
    {
        static public void Run(EnableSourceArgs args, Func<ILogger> getLogger)
        {
            var settings = RunnerHelper.GetSettings(args.Configfile);
            var sourceProvider = RunnerHelper.GetSourceProvider(settings);
            RunnerHelper.EnableOrDisableSource(sourceProvider, args.Name, enabled: true, getLogger);
        }
    }


    public partial class ListSourceRunner
    {
        static public void Run(ListSourceArgs args, Func<ILogger> getLogger)
        {
            SourcesListFormat format;
            if (string.IsNullOrEmpty(args.Format))
            {
                format = SourcesListFormat.Detailed;
            }
            else
            {
                Enum.TryParse<SourcesListFormat>(args.Format, out format);
            }

            switch (format)
            {
                case SourcesListFormat.Detailed:
                    {
                        var settings = RunnerHelper.GetSettings(args.Configfile);
                        var sourceProvider = RunnerHelper.GetSourceProvider(settings);

                        var sourcesList = sourceProvider.LoadPackageSources().ToList();
                        if (!sourcesList.Any())
                        {
                            getLogger().LogMinimal(string.Format(CultureInfo.CurrentCulture,
                                                Strings.SourcesCommandNoSources));

                            return;
                        }

                        getLogger().LogMinimal(string.Format(CultureInfo.CurrentCulture, Strings.SourcesCommandRegisteredSources));
                        var sourcePadding = new string(' ', 6);
                        for (var i = 0; i < sourcesList.Count; i++)
                        {
                            var source = sourcesList[i];
                            var indexNumber = i + 1;
                            var namePadding = new string(' ', i >= 9 ? 1 : 2);

                            getLogger().LogMinimal(string.Format(
                                "  {0}.{1}{2} [{3}]",
                                indexNumber,
                                namePadding,
                                source.Name,
                                source.IsEnabled ? string.Format(CultureInfo.CurrentCulture, Strings.SourcesCommandEnabled) : string.Format(CultureInfo.CurrentCulture, Strings.SourcesCommandDisabled)));
                            getLogger().LogMinimal(string.Format("{0}{1}", sourcePadding, source.Source));
                        }
                    }
                    break;
                case SourcesListFormat.Short:
                    {
                        var settings = RunnerHelper.GetSettings(args.Configfile);
                        var sourceProvider = RunnerHelper.GetSourceProvider(settings);

                        var sourcesList = sourceProvider.LoadPackageSources();

                        foreach (var source in sourcesList)
                        {
                            string legend = source.IsEnabled ? "E" : "D";

                            if (source.IsMachineWide)
                            {
                                legend += "M";
                            }
                            if (source.IsOfficial)
                            {
                                legend += "O";
                            }
                            legend += " ";
                            getLogger().LogMinimal(legend + source.Source);
                        }
                    }
                    break;
                case SourcesListFormat.None:
                    // TODO: which exception message?
                    throw new NotImplementedException();
            }
        }
    }


    public partial class RemoveSourceRunner
    {
        static public void Run(RemoveSourceArgs args, Func<ILogger> getLogger)
        {
            var settings = RunnerHelper.GetSettings(args.Configfile);
            var sourceProvider = RunnerHelper.GetSourceProvider(settings);

            // Check to see if we already have a registered source with the same name or source
            var source = sourceProvider.GetPackageSourceByName(args.Name);
            if (source == null)
            {
                throw new CommandException(Strings.SourcesCommandNoMatchingSourcesFound, args.Name);
            }

            sourceProvider.RemovePackageSource(args.Name);
            getLogger().LogMinimal(string.Format(CultureInfo.CurrentCulture,
                Strings.SourcesCommandSourceRemovedSuccessfully, args.Name));
        }
    }


    public partial class UpdateSourceRunner
    {
        static public void Run(UpdateSourceArgs args, Func<ILogger> getLogger)
        {
            var settings = RunnerHelper.GetSettings(args.Configfile);
            var sourceProvider = RunnerHelper.GetSourceProvider(settings);

            var existingSource = sourceProvider.GetPackageSourceByName(args.Name);
            if (existingSource == null)
            {
                throw new CommandException(Strings.SourcesCommandNoMatchingSourcesFound, args.Name);
            }

            if (!string.IsNullOrEmpty(args.Source) && !existingSource.Source.Equals(args.Source, StringComparison.OrdinalIgnoreCase))
            {
                if (!PathValidator.IsValidSource(args.Source))
                {
                    throw new CommandException(Strings.SourcesCommandInvalidSource);
                }

                // If the user is updating the source, verify we don't have a duplicate.
                var duplicateSource = sourceProvider.GetPackageSourceBySource(args.Source);
                if (duplicateSource != null)
                {
                    throw new CommandException(Strings.SourcesCommandUniqueSource);
                }

                existingSource = new Configuration.PackageSource(args.Source, existingSource.Name);
            }

            RunnerHelper.ValidateCredentials(args.Username, args.Password, args.ValidAuthenticationTypes);

            if (!string.IsNullOrEmpty(args.Username))
            {
                var hasExistingAuthTypes = existingSource.Credentials?.ValidAuthenticationTypes.Any() ?? false;
                if (hasExistingAuthTypes && string.IsNullOrEmpty(args.ValidAuthenticationTypes))
                {
                    getLogger().LogMinimal(string.Format(CultureInfo.CurrentCulture,
                        Strings.SourcesCommandClearingExistingAuthTypes, args.Name));
                }

                var credentials = Configuration.PackageSourceCredential.FromUserInput(
                    args.Name,
                    args.Username,
                    args.Password,
                    args.StorePasswordInClearText,
                    args.ValidAuthenticationTypes);
                existingSource.Credentials = credentials;
            }

            sourceProvider.UpdatePackageSource(existingSource, updateCredentials: existingSource.Credentials != null, updateEnabled: false);

            getLogger().LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandUpdateSuccessful, args.Name));
        }
    }

        internal static class RunnerHelper
    {
        public static ISettings GetSettings(string configfile)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (string.IsNullOrEmpty(configfile))
            {
                // Use settings based on probing given currentDirectory
                return NuGet.Configuration.Settings.LoadDefaultSettings(currentDirectory,
                    configFileName: null,
                    machineWideSettings: new XPlatMachineWideSetting());
            }
            else
            {
                // Use ConfigFile only
                var configFileFullPath = Path.GetFullPath(configfile);
                var configDirectory = Path.GetDirectoryName(configFileFullPath);
                var configFileName = Path.GetFileName(configFileFullPath);

                return NuGet.Configuration.Settings.LoadSpecificSettings(configDirectory,
                    configFileName: configFileName);
            }
        }

        public static PackageSourceProvider GetSourceProvider(ISettings settings)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var sourceProvider = new PackageSourceProvider(settings, enablePackageSourcesChangedEvent: false);
#pragma warning restore CS0618 // Type or member is obsolete
            return sourceProvider;
        }

        public static void EnableOrDisableSource(PackageSourceProvider sourceProvider, string name, bool enabled, Func<ILogger> getLogger)
        {
            var packageSource = sourceProvider.GetPackageSourceByName(name);
            if (packageSource == null)
            {
                throw new CommandException(Strings.SourcesCommandNoMatchingSourcesFound, name);
            }

            if (enabled && !packageSource.IsEnabled)
            {
                sourceProvider.EnablePackageSource(name);
            }
            else if (!enabled && packageSource.IsEnabled)
            {
                sourceProvider.DisablePackageSource(name);
            }

            if (enabled)
            {
                getLogger().LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandSourceEnabledSuccessfully, name));
            }
            else
            {
                getLogger().LogMinimal(string.Format(CultureInfo.CurrentCulture,
                    Strings.SourcesCommandSourceDisabledSuccessfully, name));
            }
        }

        public static void ValidateCredentials(string username, string password, string validAuthenticationTypes)
        {
            var isUsernameEmpty = string.IsNullOrEmpty(username);
            var isPasswordEmpty = string.IsNullOrEmpty(password);
            var isAuthTypesEmpty = string.IsNullOrEmpty(validAuthenticationTypes);

            if (isUsernameEmpty ^ isPasswordEmpty)
            {
                // If only one of them is set, throw.
                throw new CommandException(Strings.SourcesCommandCredentialsRequired);
            }

            if (isPasswordEmpty && !isAuthTypesEmpty)
            {
                // can't specify auth types without credentials
                throw new CommandException(Strings.SourcesCommandCredentialsRequiredWithAuthTypes);
            }
        }
    }

}

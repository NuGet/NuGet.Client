@echo off

msbuild /v:M Build\Build.tasks /t:DownloadNuGetExe

if not exist %~dp0\packages\NuGet.CommandLine.Cmd (
    msbuild %~dp0\build\build.tasks /t:DownloadNuGetExe /v:Q
    .nuget\nuget.exe install -Source https://www.myget.org/F/nugetbuild/api/v2 NuGet.CommandLine.Cmd -ExcludeVersion -Out %~dp0\packages -NoCache -pre
)

if not exist %~dp0\packages\NuGet.MsBuild.Integration (
    msbuild %~dp0\build\build.tasks /t:DownloadNuGetExe /v:Q
    .nuget\nuget.exe install -source https://www.myget.org/F/nugetbuild/api/v2 NuGet.MsBuild.Integration -ExcludeVersion -Out %~dp0\packages -NoCache -pre
)

set NuGetCommandLineCmd=%~dp0\packages\NuGet.CommandLine.Cmd\build\NuGet.CommandLine.cmd
call %NuGetCommandLineCmd% restore %~dp0\src\VisualStudioAPI\VisualStudioAPI.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\PackageManagement.VisualStudio\PackageManagement.VisualStudio.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\PackageManagement.PowerShellCmdlets\PackageManagement.PowerShellCmdlets.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\TeamFoundationServer12\TeamFoundationServer12.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\TeamFoundationServer14\TeamFoundationServer14.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\VsConsole\Console.Types\Console.Types.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\VsConsole\Console\Console.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\VsConsole\PowerShellHost\PowerShellHost.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\VsConsole\PowerShellHostProvider\PowerShellHostProvider.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\NuGet.VisualStudio.Implementation\NuGet.VisualStudio.Implementation.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\Options\Options.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

call %NuGetCommandLineCmd% restore %~dp0\src\VsExtension\VsExtension.csproj
IF %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

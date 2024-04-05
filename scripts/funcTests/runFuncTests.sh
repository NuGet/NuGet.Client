#!/usr/bin/env bash

UNIT_TESTS=0
FUNCTIONAL_TESTS=0
MONO_TESTS=0

while true ; do
    case "$1" in
        -u|--unit-tests) UNIT_TESTS=1 ; shift ;;
        -f|--functional-tests) FUNCTIONAL_TESTS=1 ; shift ;;
        -m|--mono-tests) MONO_TESTS=1 ; shift ;;
        --) shift ; break ;;
        *) shift ; break ;;
    esac
done

# move up to the repo root
SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DIR=$SCRIPTDIR/../..
pushd $DIR/

LOG_DIRECTORY=$BUILD_STAGINGDIRECTORY
if [ "$LOG_DIRECTORY" == "" ]; then
    LOG_DIRECTORY="$(pwd)/.test"

mono --version

dotnet --info

# Download the CLI install script to cli
echo "Installing dotnet CLI"
mkdir -p cli
curl -o cli/dotnet-install.sh -L https://dot.net/v1/dotnet-install.sh

# Run install.sh
chmod +x cli/dotnet-install.sh

# Disable .NET CLI Install Lookup
DOTNET_MULTILEVEL_LOOKUP=0

DOTNET="$(pwd)/cli/dotnet"

# Let the dotnet cli expand and decompress first if it's a first-run
$DOTNET --info

# Get CLI Branches for testing
echo "dotnet msbuild build/config.props /restore:false /ConsoleLoggerParameters:Verbosity=Minimal;NoSummary;ForceNoAlign /nologo /target:GetCliBranchForTesting"

IFS=$'\n'
CMD_OUT_LINES=(`dotnet msbuild build/config.props /restore:false "/ConsoleLoggerParameters:Verbosity=Minimal;NoSummary;ForceNoAlign" /nologo /target:GetCliBranchForTesting`)
# Take only last the line which has the version information and strip all the spaces
CMD_LAST_LINE=${CMD_OUT_LINES[@]:(-1)}
DOTNET_BRANCHES=${CMD_LAST_LINE//[[:space:]]}
unset IFS

IFS=$';'
for DOTNET_BRANCH in ${DOTNET_BRANCHES[@]}
do
    echo $DOTNET_BRANCH

    IFS=$':'
    ChannelAndVersion=($DOTNET_BRANCH)
    Channel=${ChannelAndVersion[0]}
    if [ ${#ChannelAndVersion[@]} -eq 1 ]
    then
        Version="latest"
    else
        Version=${ChannelAndVersion[1]}
    fi
    unset IFS

	echo "cli/dotnet-install.sh --install-dir cli --channel $Channel --version $Version -nopath"
	cli/dotnet-install.sh --install-dir cli --channel $Channel --version $Version -nopath
 
	if (( $? )); then
		echo "The .NET CLI Install for $DOTNET_BRANCH failed!!"
		exit 1
	fi
done

# Display .NET CLI info
$DOTNET --info
if (( $? )); then
    echo "DOTNET --info failed!!"
    exit 1
fi

EXIT_CODE=0

if [ "$UNIT_TESTS" == "1" ]; then
    echo "=============== Build and run unit tests started at `date -u +"%Y-%m-%dT%H:%M:%S"` ================="
    echo "dotnet msbuild build/build.proj /restore:false /target:CoreUnitTests /property:Configuration=Release /property:ReleaseLabel=beta /bl:$LOG_DIRECTORY/binlog/03.CoreUnitTests.binlog"
    dotnet msbuild build/build.proj /restore:false /target:CoreUnitTests /property:Configuration=Release /property:ReleaseLabel=beta /bl:$LOG_DIRECTORY/binlog/03.CoreUnitTests.binlog
    EXIT_CODE=$?
    echo "=============== Build and run unit tests finished at `date -u +"%Y-%m-%dT%H:%M:%S"`================="
fi

if [ "$FUNCTIONAL_TESTS" == "1" ]; then
    echo "============ Build and run functional tests started at `date -u +"%Y-%m-%dT%H:%M:%S"` =============="
    echo "dotnet msbuild build/build.proj /restore:false /target:CoreFuncTests /property:Configuration=Release /property:ReleaseLabel=beta /bl:$LOG_DIRECTORY/binlog/04.CoreFuncTests.binlog"
    dotnet msbuild build/build.proj /restore:false /target:CoreFuncTests /property:Configuration=Release /property:ReleaseLabel=beta /bl:$LOG_DIRECTORY/binlog/04.CoreFuncTests.binlog
    EXIT_CODE=$?
    echo "============== Build and run functional tests finished at `date -u +"%Y-%m-%dT%H:%M:%S"`============"
fi

if [ "$MONO_TESTS" == "1" ]; then
    # Run mono test
    TestDir="$DIR/artifacts/NuGet.CommandLine.Test/"
    VsTestConsole="$DIR/artifacts/NuGet.CommandLine.Test/vstest/vstest.console.exe"
    TestResultsDir="$DIR/build/TestResults"
    VsTestVerbosity="minimal"

echo "cli/dotnet-install.sh --install-dir cli --runtime dotnet --channel 3.1 -nopath"
cli/dotnet-install.sh --install-dir cli --runtime dotnet --channel 3.1 -nopath

    #Clean System dll
    rm -rf "$TestDir/System.*" "$TestDir/WindowsBase.dll" "$TestDir/Microsoft.CSharp.dll" "$TestDir/Microsoft.Build.Engine.dll"

    case "$(uname -s)" in
		    Linux)
			    # We are not testing Mono on linux currently, so comment it out.
			    #echo "mono $VsTestConsole $TestDir/NuGet.CommandLine.Test.dll --TestCaseFilter:Platform!=Windows&Platform!=Darwin --logger:console;verbosity=$VsTestVerbosity --logger:"trx" --ResultsDirectory:$TestResultsDir"
			    #mono $VsTestConsole "$TestDir/NuGet.CommandLine.Test.dll" --TestCaseFilter:"Platform!=Windows&Platform!=Darwin" --logger:"console;verbosity=$VsTestVerbosity" --logger:"trx" --ResultsDirectory:"$TestResultsDir"
			    #EXIT_CODE=$?
			    ;;
		    Darwin)
                echo "==================== Run mono tests started at `date -u +"%Y-%m-%dT%H:%M:%S"` ======================"
			    echo "mono $VsTestConsole $TestDir/NuGet.CommandLine.Test.dll --TestCaseFilter:Platform!=Windows&Platform!=Linux --logger:console;verbosity=$VsTestVerbosity --logger:"trx" --ResultsDirectory:$TestResultsDir"
			    mono $VsTestConsole "$TestDir/NuGet.CommandLine.Test.dll" --TestCaseFilter:"Platform!=Windows&Platform!=Linux" --logger:"console;verbosity=$VsTestVerbosity" --logger:"trx" --ResultsDirectory:"$TestResultsDir"
			    EXIT_CODE=$?
                echo "================== mono tests finished at `date -u +"%Y-%m-%dT%H:%M:%S"` ==================="
                echo ""
			    ;;
		    *) ;;
    esac
fi

# Display .NET CLI info
$DOTNET --info
if (( $? )); then
    echo "DOTNET --info failed!!"
    exit 1
fi

echo "initial dotnet cli install finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"

echo "================="

echo "Deleting .NET Core temporary files"
rm -rf "/tmp/"dotnet.*

echo "second dotnet cli install finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"
echo "================="

#restore solution packages
dotnet msbuild -t:restore "$DIR/build/bootstrap.proj" -bl:"$BUILD_STAGINGDIRECTORY/binlog/01.RestoreBootstrap.binlog"
if [ $? -ne 0 ]; then
    echo "Restore failed!!"
    exit 1
fi

echo "bootstrap project restore finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"

# init the repo

git submodule init
git submodule update

echo "git submodules updated finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"

# clear caches
if [ "$CLEAR_CACHE" == "1" ]
then
    # echo "Clearing the nuget web cache folder"
    # rm -r -f ~/.local/share/NuGet/*

    echo "Clearing the nuget packages folder"
    rm -r -f ~/.nuget/packages/*
fi

# restore packages
echo "dotnet msbuild build/build.proj /restore:false /target:Restore /property:Configuration=Release /property:ReleaseLabel=beta /bl:$BUILD_STAGINGDIRECTORY/binlog/02.Restore.binlog"
dotnet msbuild build/build.proj /restore:false /target:Restore /target:Restore /property:Configuration=Release /property:ReleaseLabel=beta /bl:$BUILD_STAGINGDIRECTORY/binlog/02.Restore.binlog

if [ $? -ne 0 ]; then
    echo "Restore failed!!"
    exit 1
fi

echo "Restore finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"

# Unit tests
echo "dotnet msbuild build/build.proj /restore:false /target:CoreUnitTests /property:Configuration=Release /property:ReleaseLabel=beta /bl:$BUILD_STAGINGDIRECTORY/binlog/03.CoreUnitTests.binlog"
dotnet msbuild build/build.proj /restore:false /target:CoreUnitTests /property:Configuration=Release /property:ReleaseLabel=beta /bl:$BUILD_STAGINGDIRECTORY/binlog/03.CoreUnitTests.binlog

if [ $? -ne 0 ]; then
    echo "CoreUnitTests failed!!"
    RESULTCODE=1
fi

echo "Core tests finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"

# Func tests
echo "dotnet msbuild build/build.proj /restore:false /target:CoreFuncTests /property:Configuration=Release /property:ReleaseLabel=beta /bl:$BUILD_STAGINGDIRECTORY/binlog/04.CoreFuncTests.binlog"
dotnet msbuild build/build.proj /restore:false /target:CoreFuncTests /property:Configuration=Release /property:ReleaseLabel=beta /bl:$BUILD_STAGINGDIRECTORY/binlog/04.CoreFuncTests.binlog

if [ $? -ne 0 ]; then
    RESULTCODE='1'
    echo "CoreFuncTests failed!!"
fi

if [ -z "$CI" ]; then
    popd
    exit $RESULTCODE
fi

#run mono test
TestDir="$DIR/artifacts/NuGet.CommandLine.Test/"
VsTestConsole="$DIR/artifacts/NuGet.CommandLine.Test/vstest/vstest.console.exe"
TestResultsDir="$DIR/build/TestResults"
VsTestVerbosity="minimal"

if [ "$SYSTEM_DEBUG" == "true" ]; then
    VsTestVerbosity="detailed"
fi

#Clean System dll
rm -rf "$TestDir/System.*" "$TestDir/WindowsBase.dll" "$TestDir/Microsoft.CSharp.dll" "$TestDir/Microsoft.Build.Engine.dll"

#Run xunit test

case "$(uname -s)" in
		Linux)
			# We are not testing Mono on linux currently, so comment it out.
			#echo "mono $VsTestConsole $TestDir/NuGet.CommandLine.Test.dll --TestCaseFilter:Platform!=Windows&Platform!=Darwin --logger:console;verbosity=$VsTestVerbosity --logger:"trx" --ResultsDirectory:$TestResultsDir"
			#mono $VsTestConsole "$TestDir/NuGet.CommandLine.Test.dll" --TestCaseFilter:"Platform!=Windows&Platform!=Darwin" --logger:"console;verbosity=$VsTestVerbosity" --logger:"trx" --ResultsDirectory:"$TestResultsDir"
			if [ $RESULTCODE -ne '0' ]; then
				RESULTCODE=$?
				echo "Unit Tests or Core Func Tests failed on Linux"
				exit 1
			fi
			;;
		Darwin)
			echo "mono $VsTestConsole $TestDir/NuGet.CommandLine.Test.dll --TestCaseFilter:Platform!=Windows&Platform!=Linux --logger:console;verbosity=$VsTestVerbosity --logger:"trx" --ResultsDirectory:$TestResultsDir"
			mono $VsTestConsole "$TestDir/NuGet.CommandLine.Test.dll" --TestCaseFilter:"Platform!=Windows&Platform!=Linux" --logger:"console;verbosity=$VsTestVerbosity" --logger:"trx" --ResultsDirectory:"$TestResultsDir"
			if [ $? -ne '0' ]; then
				RESULTCODE=$?
				echo "Mono tests failed!"
				exit 1
			fi
			;;
		*) ;;
esac

echo "Func tests finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"

popd

if [ $EXIT_CODE -eq 0 ]; then
    echo "SUCCESS!"
else
    echo "FAILED!"
fi

exit $EXIT_CODE

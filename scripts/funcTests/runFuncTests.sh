#!/usr/bin/env bash

echo "Starting runFuncTests at `date -u +"%Y-%m-%dT%H:%M:%S"`"

env | sort

while true ; do
	case "$1" in
		-c|--clear-cache) CLEAR_CACHE=1 ; shift ;;
		--) shift ; break ;;
		*) shift ; break ;;
	esac
done

RESULTCODE=0

# print openssl version
echo "==================================================================================================="
openssl version -a
echo "==================================================================================================="
# move up to the repo root
SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DIR=$SCRIPTDIR/../..
pushd $DIR/

mono --version

dotnet --info

# Download the CLI install script to cli
echo "Installing dotnet CLI"
mkdir -p cli
# Issue 8936 - DISABLED TEMPORARILY curl -o cli/dotnet-install.sh -L https://dot.net/v1/dotnet-install.sh

# Run install.sh
# Issue 8936 chmod +x cli/dotnet-install.sh
chmod +x scripts/funcTests/dotnet-install.sh

# Get recommended version for bootstrapping testing version
# Issue 8936 - DISABLED TEMPORARILY cli/dotnet-install.sh -i cli -c 2.2
scripts/funcTests/dotnet-install.sh -i cli -c 2.2 -nopath

if (( $? )); then
	echo "The .NET CLI Install failed!!"
	exit 1
fi

# Disable .NET CLI Install Lookup
DOTNET_MULTILEVEL_LOOKUP=0

DOTNET="$(pwd)/cli/dotnet"

# Let the dotnet cli expand and decompress first if it's a first-run
$DOTNET --info

# Get CLI Branches for testing
echo "dotnet msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting"

IFS=$'\n'
CMD_OUT_LINES=(`dotnet msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting`)
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

	echo "Channel is: $Channel"
	echo "Version is: $Version"
	scripts/funcTests/dotnet-install.sh -i cli -c $Channel -v $Version -nopath

	if (( $? )); then
		echo "The .NET CLI Install for $DOTNET_BRANCH failed!!"
		exit 1
	fi
done

# Display .NET CLI info
$DOTNET --info

echo "initial dotnet cli install finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"

echo "================="

echo "Deleting .NET Core temporary files"
rm -rf "/tmp/"dotnet.*

echo "second dotnet cli install finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"
echo "================="

#restore solution packages
dotnet msbuild -t:restore "$DIR/build/bootstrap.proj"
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
echo "dotnet msbuild build/build.proj /t:Restore /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
dotnet msbuild build/build.proj /t:Restore /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

echo "Restore finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"

# Unit tests
echo "dotnet msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
dotnet msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

if [ $? -ne 0 ]; then
	echo "CoreUnitTests failed!!"
	RESULTCODE=1
fi

RESULTFILE="build/TestResults/TestResults.xml"

echo "Checking if result file exists at $DIR/$RESULTFILE"
if [ -f  "$DIR/$RESULTFILE" ]
then
	echo "Renaming $DIR/$RESULTFILE"
	mv "$RESULTFILE" "$DIR/build/TestResults/TestResults.$(date +%H%M%S).xml"
else
	echo "$DIR/$RESULTFILE not found."
fi

echo "Core tests finished at `date -u +"%Y-%m-%dT%H:%M:%S"`"

# Func tests
echo "dotnet msbuild build/build.proj /t:CoreFuncTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
dotnet msbuild build/build.proj /t:CoreFuncTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

if [ $? -ne 0 ]; then
	RESULTCODE='1'
	echo "CoreFuncTests failed!!"
fi

echo "Checking if result file exists at $DIR/$RESULTFILE"
if [ -f  "$DIR/$RESULTFILE" ]
then
	echo "Renaming $DIR/$RESULTFILE"
	mv "$RESULTFILE" "$DIR/build/TestResults/TestResults.$(date +%H%M%S).xml"
else
	echo "$DIR/$RESULTFILE not found."
fi

if [ -z "$CI" ]; then
	popd
	exit $RESULTCODE
fi

#run mono test
TestDir="$DIR/artifacts/NuGet.CommandLine.Test/"
XunitConsole="$DIR/packages/xunit.runner.console/2.4.1/tools/net472/xunit.console.exe"

#Clean System dll
rm -rf "$TestDir/System.*" "$TestDir/WindowsBase.dll" "$TestDir/Microsoft.CSharp.dll" "$TestDir/Microsoft.Build.Engine.dll"

#Run xunit test

case "$(uname -s)" in
		Linux)
			# We are not testing Mono on linux currently, so comment it out.
			#echo "mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Darwin -xml build/TestResults/monoonlinux.xml -verbose"
			#mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Darwin -xml "build/TestResults/monoonlinux.xml" -verbose
			if [ $RESULTCODE -ne '0' ]; then
				RESULTCODE=$?
				echo "Unit Tests or Core Func Tests failed on Linux"
				exit 1
			fi
			;;
		Darwin)
			# Disable Mono tests on Mac by default. Uncomment to run them.
			#echo "mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Linux -xml build/TestResults/monoomac.xml -verbose"
			#mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Linux -xml "build/TestResults/monoonmac.xml" -verbose
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

exit $RESULTCODE

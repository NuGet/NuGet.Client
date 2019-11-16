#!/usr/bin/env bash

env | sort

while true ; do
	case "$1" in
		-c|--clear-cache) CLEAR_CACHE=1 ; shift ;;
		--) shift ; break ;;
		*) shift ; break ;;
	esac
done

RESULTCODE=0

# move up to the repo root
SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DIR=$SCRIPTDIR/../..
pushd $DIR/

mono --version

# Download the CLI install script to cli
echo "Installing dotnet CLI"
mkdir -p cli
curl -o cli/dotnet-install.sh -L https://dot.net/v1/dotnet-install.sh

# Run install.sh
chmod +x cli/dotnet-install.sh

# Get recommended version for bootstrapping testing version
cli/dotnet-install.sh -i cli

DOTNET="$(pwd)/cli/dotnet"

#restore solution packages
$DOTNET msbuild -t:restore "$DIR/build/bootstrap.proj"
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

echo "$DOTNET msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting"
DOTNET_BRANCH="$($DOTNET msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting)"

echo $DOTNET_BRANCH
cli/dotnet-install.sh -i cli -v $DOTNET_BRANCH

# Install the 2.x runtime because our tests target netcoreapp2x
cli/dotnet-install.sh -runtime dotnet -Channel 2.2 -i cli -NoPath
# Display current version
$DOTNET --version

echo "Deleting .NET Core temporary files"
rm -rf "/tmp/"dotnet.*

echo "================="

# init the repo

git submodule init
git submodule update

# clear caches
if [ "$CLEAR_CACHE" == "1" ]
then
	# echo "Clearing the nuget web cache folder"
	# rm -r -f ~/.local/share/NuGet/*

	echo "Clearing the nuget packages folder"
	rm -r -f ~/.nuget/packages/*
fi

# restore packages
echo "$DOTNET msbuild build/build.proj /t:Restore /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
$DOTNET msbuild build/build.proj /t:Restore /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

# Unit tests
echo "$DOTNET msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
$DOTNET msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

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

# Func tests
echo "$DOTNET msbuild build/build.proj /t:CoreFuncTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
$DOTNET msbuild build/build.proj /t:CoreFuncTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

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
XunitConsole="$DIR/packages/xunit.runner.console/2.4.1/tools/net452/xunit.console.exe"

#Clean System dll
rm -rf "$TestDir/System.*" "$TestDir/WindowsBase.dll" "$TestDir/Microsoft.CSharp.dll" "$TestDir/Microsoft.Build.Engine.dll"

#Run xunit test

case "$(uname -s)" in
		Linux)
			# We are not testing Mono on linux currently, so comment it out.
			#echo "mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Darwin -xml build/TestResults/monoonlinux.xml"
			#mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Darwin -xml "build/TestResults/monoonlinux.xml"
			if [ $RESULTCODE -ne '0' ]; then
				RESULTCODE=$?
				echo "Unit Tests or Core Func Tests failed on Linux"
				exit 1
			fi
			;;
		Darwin)
			echo "mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Linux -xml build/TestResults/monoomac.xml -verbose"
			mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Linux -xml "build/TestResults/monoonmac.xml" -verbose
			if [ $? -ne '0' ]; then
				RESULTCODE=$?
				echo "Mono tests failed!"
				exit 1
			fi
			;;
		*) ;;
esac


popd

exit $RESULTCODE

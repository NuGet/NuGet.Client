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

# print openssl version
echo "==================================================================================================="
openssl version -a
echo "==================================================================================================="
# move up to the repo root
SCRIPTDIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
DIR=$SCRIPTDIR/../..
pushd $DIR/

mono --version

# Download the CLI install script to cli
echo "Installing dotnet CLI"
mkdir -p cli
# Issue 8936 - DISABLED TEMPORARILY curl -o cli/dotnet-install.sh -L https://dot.net/v1/dotnet-install.sh

# Run install.sh
# Issue 8936 chmod +x cli/dotnet-install.sh
chmod +x scripts/funcTests/dotnet-install.sh

# Get recommended version for bootstrapping testing version
# Issue 8936 - DISABLED TEMPORARILY cli/dotnet-install.sh -i cli -c 2.2
scripts/funcTests/dotnet-install.sh -i cli -c 2.2 -NoPath
# cli/dotnet-install.sh -runtime dotnet -Channel 2.2 -i cli -NoPath

DOTNET="$(pwd)/cli/dotnet"

echo "dotnet msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting"

# run it twice so dotnet cli can expand and decompress without affecting the result of the target
dotnet msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting
DOTNET_BRANCHES="$(dotnet msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting)"
echo $DOTNET_BRANCHES | tr ";" "\n" |  while read -r DOTNET_BRANCH
do
	echo $DOTNET_BRANCH
	ChannelAndVersion=($DOTNET_BRANCH)
	Channel=${ChannelAndVersion[0]}
	if [ ${#ChannelAndVersion[@]} -eq 1 ]
	then
		Version="latest"
	else
		Version=${ChannelAndVersion[1]}
	fi
	echo "Channel is: $Channel"
	echo "Version is: $Version"
	# Issue 8936 - DISABLED TEMPORARILY cli/dotnet-install.sh -i cli -c 2.2
    scripts/funcTests/dotnet-install.sh -i cli -c $Channel -v $Version -nopath
	#cli/dotnet-install.sh -i cli -c $Channel -v $Version -nopath

	# Display current version
	$DOTNET --version
	dotnet --info
done
echo "================="

# install SDK2 runtime as we encounter problems on running dotnet vstest command when only download SDK3.
cli/dotnet-install.sh -runtime dotnet -Channel 2.2 -i cli -NoPath

echo "Deleting .NET Core temporary files"
rm -rf "/tmp/"dotnet.*

echo "================="

#restore solution packages
$DOTNET msbuild -t:restore "$DIR/build/bootstrap.proj"
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

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
echo "dotnet msbuild build/build.proj /t:Restore /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
dotnet msbuild build/build.proj /t:Restore /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

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

#!/usr/bin/env bash
env
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
DIR=$SCRIPTDIR/../../
pushd $DIR

# Download the CLI install script to cli
echo "Installing dotnet CLI"
mkdir -p cli
curl -o cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/d2bbe1faa294012cec60b640e6522e0674224d3f/scripts/obtain/dotnet-install.sh

# Run install.sh
chmod +x cli/dotnet-install.sh
cli/dotnet-install.sh -i cli -c preview -v 1.0.1

# Display current version
DOTNET="$(pwd)/cli/dotnet"
$DOTNET --version

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
echo "$DOTNET msbuild build/build.proj /t:RestoreTests /p:VisualStudioVersion=15.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
$DOTNET msbuild build/build.proj /t:Restore /p:VisualStudioVersion=15.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

# Unit tests
echo "$DOTNET msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=15.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
$DOTNET msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=15.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

if [ $? -ne 0 ]; then
	echo "CoreUnitTests failed!!"
	exit 1
fi

# Func tests
echo "$DOTNET msbuild build/build.proj /t:CoreFuncTests /p:VisualStudioVersion=15.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
$DOTNET msbuild build/build.proj /t:CoreFuncTests /p:VisualStudioVersion=15.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

if [ $? -ne 0 ]; then
	echo "CoreFuncTests failed!!"
	exit 1
fi

if [ -z "$CI" ]; then
	popd
	exit $RESULTCODE
fi

#run mono test
TestDir="$DIR/artifacts/NuGet.CommandLine.Test/"
XunitConsole="$DIR/packages/xunit.runner.console.2.2.0/tools/xunit.console.exe"
NuGetExe="$DIR/.nuget/nuget.exe"

#Get NuGet.exe
curl -o $NuGetExe https://dist.nuget.org/win-x86-commandline/latest-prerelease/nuget.exe

mono --version

#restore solution packages
mono $NuGetExe restore  "$DIR/.nuget/packages.config" -SolutionDirectory "$DIR"
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

#Clean System dll
rm -r -f "$TestDir/System.*" "$TestDir/WindowsBase.dll" "$TestDir/Microsoft.CSharp.dll" "$TestDir/Microsoft.Build.Engine.dll"

#Run xunit test

case "$(uname -s)" in
		Linux)
			# We are not testing Mono on linux currently, so comment it out.
			#echo "mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.exe" -notrait Platform=Windows -notrait Platform=Darwin -xml build/TestResults/monoonlinux.xml"
			#mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.exe" -notrait Platform=Windows -notrait Platform=Darwin -xml "build/TestResults/monoonlinux.xml"
			;;
		Darwin)
			echo "mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.exe" -notrait Platform=Windows -notrait Platform=Linux -xml build/TestResults/monoomac.xml"
			mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.exe" -notrait Platform=Windows -notrait Platform=Linux -xml "build/TestResults/monoonmac.xml"
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

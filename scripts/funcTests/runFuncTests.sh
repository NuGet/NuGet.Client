#!/usr/bin/env bash

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
pushd DIR

# Download the CLI install script to cli
echo "Installing dotnet CLI"
mkdir -p cli
curl -o cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview2/scripts/obtain/dotnet-install.sh

# Run install.sh
chmod +x cli/dotnet-install.sh
cli/dotnet-install.sh -i cli -c preview -v 1.0.0-preview2-003121

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
	# echo "Clearing the nuget cache folder"
	# rm -r -f ~/.local/share/NuGet/*

	echo "Clearing the nuget packages folder"
	rm -r -f ~/.nuget/packages/*
fi

# restore packages
$DOTNET restore src/NuGet.Core test/NuGet.Core.Tests test/NuGet.Core.FuncTests --verbosity minimal
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

# run tests
for testProject in `find test/NuGet.Core.FuncTests -type f -name project.json`
do
	testDir="$(pwd)/$(dirname $testProject)"

	if grep -q netcoreapp1.0 "$testProject"; then
		pushd $testDir

		case "$(uname -s)" in
			Linux)
				echo "$DOTNET test $testDir --configuration release --framework netcoreapp1.0 -notrait Platform=Windows -notrait Platform=Darwin"
				$DOTNET test $testDir --configuration release --framework netcoreapp1.0 -notrait Platform=Windows -notrait Platform=Darwin
				;;
			Darwin)
				echo "$DOTNET test $testDir --configuration release --framework netcoreapp1.0 -notrait Platform=Windows -notrait Platform=Linux"
				$DOTNET test $testDir --configuration release --framework netcoreapp1.0 -notrait Platform=Windows -notrait Platform=Linux
				;;
			*) ;;
		esac

		if [ $? -ne 0 ]; then
			echo "$testDir FAILED on CoreCLR"
			RESULTCODE=1
		fi

		popd
	else
		echo "Skipping the tests in $testProject on CoreCLR"
	fi
done

#run mono test
TestDir="$DIR/artifacts/NuGet.CommandLine.Test/14.0/Release"
XunitConsole="$DIR/packages/xunit.runner.console.2.1.0/tools/xunit.console.exe"
NuGetExe="$DIR/.nuget/nuget.exe"

#Get NuGet.exe
wget -O $NuGetExe https://dist.nuget.org/win-x86-commandline/latest-prerelease/nuget.exe

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
			echo "mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Darwin"
			mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Darwin
			;;
		Darwin)
			echo "mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Linux"
			mono $XunitConsole "$TestDir/NuGet.CommandLine.Test.dll" -notrait Platform=Windows -notrait Platform=Linux
			;;
		*) ;;
esac


popd

exit $RESULTCODE

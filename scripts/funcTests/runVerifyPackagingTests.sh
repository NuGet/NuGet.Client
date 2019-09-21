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

NuGetExe="$DIR/.nuget/nuget.exe"
#Get NuGet.exe
curl -o $NuGetExe https://dist.nuget.org/win-x86-commandline/latest/nuget.exe

mono --version

#restore solution packages
mono $NuGetExe restore  "$DIR/.nuget/packages.config" -SolutionDirectory "$DIR"
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

# Download the CLI install script to cli
echo "Installing dotnet CLI"
mkdir -p cli
curl -o cli/dotnet-install.sh https://dot.net/v1/dotnet-install.sh

# Run install.sh
chmod +x cli/dotnet-install.sh

# Get recommended version for bootstrapping testing version
cli/dotnet-install.sh -i cli -NoPath

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
	cli/dotnet-install.sh -i cli -c $Channel -v $Version -nopath

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
echo "dotnet msbuild build/build.proj /t:RestoreCrossVerifyTest /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
dotnet msbuild build/build.proj /t:RestoreCrossVerifyTest /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

# Generate signed packages tests
echo "dotnet msbuild build/build.proj /t:CrossVerifyTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
dotnet msbuild build/build.proj /t:CrossVerifyTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

if [ $? -ne 0 ]; then
	echo "CoreUnitTests failed!!"
	RESULTCODE=1
fi

RESULTFILE="build/TestResults/TestResults.xml"

echo "Checking if result file exists at $DIR$RESULTFILE"
if [ -f  "$DIR$RESULTFILE" ]
then
	echo "Renaming $DIR$RESULTFILE"
	mv "$RESULTFILE" "$DIR/build/TestResults/TestResults.$(date +%H%M%S).xml"
else
	echo "$DIR$RESULTFILE not found."
fi

if [ -z "$CI" ]; then
	popd
	exit $RESULTCODE
fi

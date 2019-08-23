#!/usr/bin/env bash

while true ; do
	case "$1" in
		-c|--clear-cache) CLEAR_CACHE=1 ; shift ;;
		--) shift ; break ;;
		*) shift ; break ;;
	esac
done

RESULTCODE=0

# Download the CLI install script to cli
echo "Installing dotnet CLI"
mkdir -p cli
curl -o cli/dotnet-install.sh -L https://dot.net/v1/dotnet-install.sh

# Run install.sh for cli
chmod +x cli/dotnet-install.sh

# v1 needed for some test and bootstrapping testing version
cli/dotnet-install.sh -i cli -c 1.0

DOTNET="$(pwd)/cli/dotnet"

echo "$DOTNET msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting"

# run it twice so dotnet cli can expand and decompress without affecting the result of the target
$DOTNET msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting
DOTNET_BRANCH="$($DOTNET msbuild build/config.props /v:m /nologo /t:GetCliBranchForTesting)"

echo $DOTNET_BRANCH
cli/dotnet-install.sh -i cli -c $DOTNET_BRANCH

# Display current version
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
echo "$DOTNET msbuild build/build.proj /t:Restore /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
$DOTNET msbuild build/build.proj /t:Restore /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

# run tests
echo "$DOTNET msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
$DOTNET msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=16.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

if [ $? -ne 0 ]; then
	echo "Tests failed!!"
	exit 1
fi

exit $RESULTCODE

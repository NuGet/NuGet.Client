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
curl -o cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/master/scripts/obtain/dotnet-install.sh

# Run install.sh for cli
chmod +x cli/dotnet-install.sh
# v1 needed for some test
cli/dotnet-install.sh -i cli -c 1.0
# todo: update to read version from build.props https://github.com/NuGet/Home/issues/7485
cli/dotnet-install.sh -i cli -c release/2.2.2xx

# Display current version
DOTNET_TEST="$(pwd)/cli_test/dotnet"
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

# run tests
echo "$DOTNET msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=15.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta"
$DOTNET msbuild build/build.proj /t:CoreUnitTests /p:VisualStudioVersion=15.0 /p:Configuration=Release /p:BuildNumber=1 /p:ReleaseLabel=beta

if [ $? -ne 0 ]; then
	echo "Tests failed!!"
	exit 1
fi

exit $RESULTCODE

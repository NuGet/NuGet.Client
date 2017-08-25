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
curl -o cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/8c5e955252f93f54e239e5f1c978700b97fc21c1/scripts/obtain/dotnet-install.sh

# Download the CLI install script to cli test
# echo "Installing dotnet CLI test"
# mkdir -p cli_test
# curl -o cli_test/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/d2bbe1faa294012cec60b640e6522e0674224d3f/scripts/obtain/dotnet-install.sh

# Run install.sh for cli
chmod +x cli/dotnet-install.sh
cli/dotnet-install.sh -i cli -c preview -v 1.0.1

# Run install.sh fot cli test
# chmod +x cli_test/dotnet-install.sh
# cli_test/dotnet-install.sh -i cli_test -c preview -v 1.0.0-rc4-004788


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

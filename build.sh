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
curl -o cli/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-preview2/scripts/obtain/dotnet-install.sh

# Download the CLI install script to cli test
echo "Installing dotnet CLI test"
mkdir -p cli_test
curl -o cli_test/dotnet-install.sh https://raw.githubusercontent.com/dotnet/cli/d2bbe1faa294012cec60b640e6522e0674224d3f/scripts/obtain/dotnet-install.sh

# Run install.sh for cli
chmod +x cli/dotnet-install.sh
cli/dotnet-install.sh -i cli -c preview -v 1.0.0-preview2-003121

# Run install.sh fot cli test
chmod +x cli_test/dotnet-install.sh
cli_test/dotnet-install.sh -i cli_test -c preview -v 1.0.0-rc4-004616


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
echo "$DOTNET restore src/NuGet.Core test/NuGet.Core.Tests --verbosity minimal"
$DOTNET restore src/NuGet.Core test/NuGet.Core.Tests --verbosity minimal
if [ $? -ne 0 ]; then
	echo "Restore failed!!"
	exit 1
fi

# build xplat dll
echo "$DOTNET build src/NuGet.Core/NuGet.CommandLine.XPlat --configuration release --framework netcoreapp1.0"
$DOTNET build src/NuGet.Core/NuGet.CommandLine.XPlat --configuration release --framework netcoreapp1.0

# run tests
for testProject in `find test/NuGet.Core.Tests -type f -name project.json`
do
	testDir="$(pwd)/$(dirname $testProject)"

	if grep -q "netcoreapp1.0" "$testProject"; then
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
		echo "Skipping the tests in $testDir on CoreCLR"
	fi

done

exit $RESULTCODE

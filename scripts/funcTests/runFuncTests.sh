#!/usr/bin/env bash

while true ; do
    case "$1" in
        -c|--clear-cache) CLEAR_CACHE=1 ; shift ;;
        --) shift ; break ;;
        *) shift ; break ;;
    esac
done

RESULTCODE=0

# install dnx
if ! type dnvm > /dev/null 2>&1; then
    source ~/.dnx/dnvm/dnvm.sh
fi

if ! type dnx > /dev/null 2>&1 || [ -z "$SKIP_DNX_INSTALL" ]; then
    dnvm install 1.0.0-rc1-update1 -runtime coreclr -alias default
    dnvm install 1.0.0-rc1-update1 -runtime mono -alias default
fi

dnvm use 1.0.0-rc1-update1 -runtime coreclr

# clear caches
if [ "$CLEAR_CACHE" == "1" ]
then
    echo "Clearing the dnu cache folder"
    rm -r -f ~/.local/share/dnu/cache/*

    echo "Clearing the dnx packages folder"
    rm -r -f ~/.dnx/packages/*
fi

# restore packages
dnu restore src/NuGet.Core
dnu restore test/NuGet.Core.FuncTests

# run tests
for testProject in `find test/NuGet.Core.FuncTests -type f -name project.json`
do
	if grep -q dnxcore50 "$testProject"; then
         echo "Running tests in $testProject on CoreCLR"

		 dnvm use 1.0.0-rc1-update1 -runtime coreclr
		 dnx --project $testProject test -parallel none

		 if [ $? -ne 0 ]; then
			echo "$testProject FAILED on CoreCLR"
			RESULTCODE=1
		 fi
	else
         echo "Skipping the tests in $testProject on CoreCLR"
	fi
done

exit $RESULTCODE
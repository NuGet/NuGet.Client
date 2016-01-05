#!/usr/bin/env bash

# export env variable
export temp="$HOME/temp"

# install dnx
if ! type dnvm > /dev/null 2>&1; then
    source ~/.dnx/dnvm/dnvm.sh
fi

if ! type dnx > /dev/null 2>&1 || [ -z "$SKIP_DNX_INSTALL" ]; then
    dnvm install 1.0.0-rc1-update1 -runtime coreclr -alias default
    dnvm install 1.0.0-rc1-update1 -runtime mono -alias default
fi

dnvm use 1.0.0-rc1-update1 -runtime coreclr

# init the repo

git submodule init
git submodule update

# restore packages
dnu restore
dnu restore test/NuGet.Core.Tests

# run tests
for testProject in `find test/NuGet.Core.Tests -type f -name project.json`
do
    if [[ $testProject =~ "NuGet.Protocol.Core.v3.Tests" ]] ||
       [[ $testProject =~ "NuGet.Resolver.Test" ]] ||
       [[ $testProject =~ "NuGet.Packaging.Test" ]] ||
       [[ $testProject =~ "NuGet.PackageManagement.Test" ]] ||
       [[ $testProject =~ "NuGet.ProjectManagement.Test" ]];
    then
        echo "Skipping tests in $testProject because they hang"
        continue
    fi

    echo "Running tests in $testProject on Mono"
    dnvm use 1.0.0-rc1-update1 -runtime mono
    dnx --project $testProject test -parallel none

    if grep -q dnxcore50 "$testProject"; then    
        echo "Running tests in $testProject on CoreCLR"
        dnvm use 1.0.0-rc1-update1 -runtime coreclr
        dnx --project $testProject test -parallel none
    else
        echo "Skipping the tests in $testProject on CoreCLR"
    fi
done

#!/bin/bash
#This script is called for Mac/Linux agents and provided 3 positional arguments - the first one being the PAT for NuGetLurker account,
#the second one is the context value for the github api and the third one is the value of env variable agent.jobstatus
echo "$1"
echo "$2"
echo "$3"
if [ "$3" == "Succeeded" ]; then
    echo "Tests succeeded"
    STATE="success"
    DESCRIPTION="Succeeded"
elif [ "$3" == "Canceled" ]; then
    echo "Tests succeeded"
    STATE="error"
    DESCRIPTION="Canceled"
else
    echo "Tests failed or were cancelled"
	STATE="failure"
    DESCRIPTION="Failed"
fi

curl -u nugetlurker:$1 https://api.github.com/repos/nuget/nuget.client/statuses/$BUILD_SOURCEVERSION --data "{\"state\":\"$STATE\",\"context\":\"$2\", \"description\":\"$DESCRIPTION\", \"target_url\":\"$BUILDURL\"}"
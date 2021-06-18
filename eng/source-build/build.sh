#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

# resolve $SOURCE until the file is no longer a symlink
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"

  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done

function ReadGlobalVersion {
  local key=$1
  local global_json_file="$scriptroot/global.json"

  if command -v jq &> /dev/null; then
    _ReadGlobalVersion="$(jq -r ".[] | select(has(\"$key\")) | .\"$key\"" "$global_json_file")"
  elif [[ "$(cat "$global_json_file")" =~ \"$key\"[[:space:]\:]*\"([^\"]+) ]]; then
    _ReadGlobalVersion=${BASH_REMATCH[1]}
  fi

  if [[ -z "$_ReadGlobalVersion" ]]; then
    Write-PipelineTelemetryError -category 'Build' "Error: Cannot find \"$key\" in $global_json_file"
    ExitWithExitCode 1
  fi
}

function GetNuGetPackageCachePath {
  if [[ -z ${NUGET_PACKAGES:-} ]]; then
    if [[ "$use_global_nuget_cache" == true ]]; then
      export NUGET_PACKAGES="$HOME/.nuget/packages"
    else
      export NUGET_PACKAGES="$repo_root/.packages"
      export RESTORENOCACHE=true
    fi
  fi
}

export DOTNET=${DOTNET:-dotnet}

ReadGlobalVersion Microsoft.DotNet.Arcade.Sdk
export ARCADE_VERSION=$_ReadGlobalVersion
"$DOTNET" msbuild "$scriptroot/source-build.proj" /p:DotNetBuildFromSource=true /p:ArcadeBuildFromSource=true "/p:RepoRoot=$scriptroot/../../" "/bl:$scriptroot/../../artifacts/source-build/self/log/source-build.binlog"

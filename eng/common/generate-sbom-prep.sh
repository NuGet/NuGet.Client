#!/usr/bin/env bash

manifest_dir=$1

if [ ! -d "$manifest_dir" ] ; then
  mkdir -p "$manifest_dir"
  echo "Created directory $manifest_dir"
fi

artifact_name="${SYSTEM_STAGENAME}_${AGENT_JOBNAME}_SBOM"
artifact_name=$(echo $artifact_name | sed 's/["/:<>\\|?@*"() ]/_/g')
echo "Artifact name $artifact_name"
echo "##vso[task.setvariable variable=ARTIFACT_NAME]$artifact_name"

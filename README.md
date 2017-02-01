
![Octopus Logo](https://i.octopus.com/blog/201605-logo-text-blueblacktransparent-400_rgb-TTE8.png)
![Heart](https://upload.wikimedia.org/wikipedia/commons/4/42/Love_Heart_SVG.svg)
![NuGet logo](https://raw.githubusercontent.com/NuGet/Home/master/resources/nuget.png)

-------------

# Custom fork of NuGet for Octopus Deploy

NuGet 3 started removing leading zeros and the fourth digit if it is zero. These are affectionately known as "NuGet zero quirks" and can be surprising when working with tooling outside the NuGet ecosystem. We have made a choice to preserve the version as-is when working with Octopus tooling to create packages of any kind. Learn more about [versioning in Octopus Deploy](http://docs.octopusdeploy.com/display/OD/Versioning+in+Octopus+Deploy).

The build is available here: http://build.octopushq.com/project.html?projectId=OctopusDeploy_NuGet&tab=projectOverview (Private)

The packages are available here: https://octopus.myget.org/feed/octopus-dependencies/package/nuget/NuGet.CommandLine. The packages we build are in the format `Nuget.<component>.<version>-octopus-<buildnumber>`. Any others are copies from NuGet. To prevent confusion (by human and tooling) we are now marking our versions as `3.6.0-octopus-<buildnumber>` as `3.6` is not present on nuget itself.

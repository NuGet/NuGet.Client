@echo off
if "%VisualStudioVersion%"=="" call %VS2012CommandPromptBat%
msbuild NuGet.VisualStudioExtension.sln /p:VisualStudioVersion="14.0" /p:DeployExtension=false /p:EnableCodeAnalysis=true /v:M /m
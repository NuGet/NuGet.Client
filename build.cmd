@echo off
if "%VisualStudioVersion%"=="" call %VS2012CommandPromptBat%
msbuild build\build.msbuild %*

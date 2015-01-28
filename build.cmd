@echo off
if "%VS120COMNTOOLS%"=="" call %VS2012CommandPromptBat%
msbuild build\build.msbuild %*

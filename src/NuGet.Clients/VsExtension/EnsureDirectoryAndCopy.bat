@echo off
if "%1"=="" goto usage
if "%2"=="" goto usage

if exist %2 goto copyfile
md %2

:copyfile
copy %1 %2
goto end

:usage
echo Usage:
echo EnsureDirectoryAndCopy (file to copy) (target dir)

:end
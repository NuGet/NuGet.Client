setlocal 
@rem Set COMPLUS_ variables to point to v2.0. This prevents lsbuild.exe from 
@rem binding to v4.0 managed toolset. Please dont unset these variable as that
@rem will require build machines to install .NET 2.0.

@rem set COMPLUS_InstallRoot=%_NTBINDIR%\tools\devdiv\loc
set COMPLUS_Version=v2.0

%_NTBINDIR%\tools\devdiv\loc\current\lsbuild.exe %*

endlocal

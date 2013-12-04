del *.nupkg
xcopy ..\LICENSE ..\deploy\noef\ /y
xcopy ..\README.md ..\deploy\noef\ /y
NuGet.exe pack noef.nuspec -BasePath ..\deploy\noef\ -Verbosity detailed

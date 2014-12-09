nuget setApiKey LamWerd66 -Source http://dev-maps.cbre.com/nugdev/
msbuild NoefGen.proj /t:PushNugetDev
@if %errorlevel% neq 0 pause && exit /b %errorlevel%

msbuild NoefGen.SourceDistro.proj /t:PushNugetDev
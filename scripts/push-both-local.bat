nuget setApiKey LamWerd66 -Source http://localhost.cbre.com/nug/

msbuild NoefGen.proj /t:PushNugetLocal
@if %errorlevel% neq 0 pause && exit /b %errorlevel%

msbuild Noef.SourceDistro.proj /t:PushNugetLocal

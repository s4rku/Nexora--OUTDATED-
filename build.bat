@echo off
taskkill /IM NEXORA.exe /F 2>nul
rd /s /q "D:\NEXORA\src\NEXORA\bin" 2>nul
rd /s /q "D:\NEXORA\src\NEXORA\obj" 2>nul
"C:\Program Files\dotnet\dotnet.exe" build "D:\NEXORA\src\NEXORA\NEXORA.csproj" -c Debug -p:Platform=x64 > "D:\NEXORA\build_out.txt" 2>&1
if %ERRORLEVEL% EQU 0 (
    echo BUILD_SUCCEEDED >> "D:\NEXORA\build_out.txt"
) else (
    echo BUILD_FAILED >> "D:\NEXORA\build_out.txt"
)

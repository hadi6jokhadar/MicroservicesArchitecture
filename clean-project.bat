@echo off
REM Clean all bin and obj folders recursively in the solution
setlocal enabledelayedexpansion

REM Set the root directory to the current directory
set "root=%cd%"

echo Cleaning bin and obj folders in %root% ...

for /d /r "%root%" %%d in (bin,obj) do (
    if exist "%%d" (
        echo Deleting folder: %%d
        rmdir /s /q "%%d"
    )
)

echo Clean complete.
pause
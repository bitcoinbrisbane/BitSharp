@echo off

for /d %%d in ("*") do (
	if exist "%%d\bin" rmdir /s /q "%%d\bin"
	if exist "%%d\obj" rmdir /s /q "%%d\obj"
)

if exist *.db del /q *.db
if exist *.log del /q *.log

if exist "packages" rmdir /s /q "packages"
if exist "TestResults" rmdir /s /q "TestResults"

if exist "BitSharp.BlockHelper\Blocks" rmdir /s /q "BitSharp.BlockHelper\Blocks"

@echo off

for /d %%d in ("*") do rmdir /s /q "%%d\bin"
for /d %%d in ("*") do rmdir /s /q "%%d\obj"

del /q *.db
del /q *.log

rmdir /s /q "packages"
rmdir /s /q "TestResults"

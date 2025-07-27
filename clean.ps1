dotnet clean
Remove-Item -Recurse -Force "bin","obj" -ErrorAction SilentlyContinue
Get-ChildItem -Recurse -Directory -Name "bin","obj" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
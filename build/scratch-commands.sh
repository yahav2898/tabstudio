set -x
dotnet run --project build/gdiplus-references -c Release -- ci-out/references 2>&1 | tail -5
powershell -ExecutionPolicy Bypass -File build/gdiplus-references/ImageListResx.ps1 ci-out/resx 2>&1 | tail -40

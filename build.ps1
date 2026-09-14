$ErrorActionPreference = "Stop"

dotnet workload restore GolfGrind.App/GolfGrind.App.csproj
dotnet restore GolfGrind.slnx
dotnet build GolfGrind.App/GolfGrind.App.csproj -f net10.0-windows10.0.19041.0 --no-restore
dotnet run --project GolfGrind.ProtocolChecks/GolfGrind.ProtocolChecks.csproj
dotnet run --project GolfGrind.CoreChecks/GolfGrind.CoreChecks.csproj

Write-Host "Windows app build, protocol checks, and Core reliability checks completed successfully." -ForegroundColor Green

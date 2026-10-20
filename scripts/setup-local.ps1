$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../server/Hexa.Api.csproj'
Write-Host 'Cau hinh SQL Server tren may ban. Nen dung database test/ban sao V8.'
$connection = Read-Host 'Connection string (Enter de dung SQL Express, Windows Authentication, database test)'
if ([string]::IsNullOrWhiteSpace($connection)) {
    $connection = 'Server=.\SQLEXPRESS;Database=QuanLyTaiSanHexa_DotNet_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
}
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
@{ 'ConnectionStrings:DefaultConnection' = $connection; 'Jwt:Key' = [Convert]::ToBase64String($bytes) } |
    ConvertTo-Json -Compress | dotnet user-secrets set --project $project
if ($LASTEXITCODE -ne 0) { throw 'Khong luu duoc cau hinh.' }
Write-Host 'Da cau hinh. Chay: dotnet run --project server'

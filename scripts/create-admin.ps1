$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../server/Hexa.Api.csproj'
$email = Read-Host 'Email admin moi (khong ghi de tai khoan da co)'
$name = Read-Host 'Ho ten admin'
$secure = Read-Host 'Mat khau (8+ ky tu, hoa/thuong/so/ky tu dac biet)' -AsSecureString
$password = [System.Net.NetworkCredential]::new('', $secure).Password
try {
    @{ 'Bootstrap:Email' = $email; 'Bootstrap:Name' = $name; 'Bootstrap:Password' = $password } |
        ConvertTo-Json -Compress | dotnet user-secrets set --project $project
    if ($LASTEXITCODE -ne 0) { throw 'Khong luu duoc cau hinh tam.' }
    dotnet run --project $project -- --create-admin
    if ($LASTEXITCODE -ne 0) { throw 'Tao admin that bai. Kiem tra SQL va email trung.' }
} finally {
    $password = $null
    dotnet user-secrets remove 'Bootstrap:Email' --project $project | Out-Null
    dotnet user-secrets remove 'Bootstrap:Name' --project $project | Out-Null
    dotnet user-secrets remove 'Bootstrap:Password' --project $project | Out-Null
}

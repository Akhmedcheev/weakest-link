$content = Get-Content "$PSScriptRoot\Views\Styles\ObsidianStyles.xaml" -Raw -Encoding UTF8
$content = $content -replace 'BorderThickness/>', 'BorderThickness"/'
$content | Set-Content "$PSScriptRoot\Views\Styles\ObsidianStyles.xaml" -Encoding UTF8 -NoNewline
Write-Host "Fixed!"

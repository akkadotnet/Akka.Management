<#
.SYNOPSIS
This is a Powershell script to bootstrap a Fake build.
.DESCRIPTION
This Powershell script will download NuGet if missing, restore NuGet tools (including Fake)
and execute your Fake build script with the parameters you provide.
.PARAMETER Target
The build script target to run.
.PARAMETER Configuration
The build configuration to use.
.PARAMETER Verbosity
Specifies the amount of information to be displayed.
.PARAMETER WhatIf
Performs a dry run of the build script.
No tasks will be executed.
.PARAMETER ScriptArgs
Remaining arguments are added here.
#>

[CmdletBinding()]
Param(
    [string]$Target = "Default",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Verbose",
    [switch]$WhatIf,
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$ScriptArgs
)

$FakeVersion = "4.61.2"
$DotNetChannel = "LTS";
$NugetVersion = "4.1.0";
$NugetUrl = "https://dist.nuget.org/win-x86-commandline/v$NugetVersion/nuget.exe"
$ProtobufVersion = "3.4.0"
$DocfxVersion = "2.49.0"

# Make sure tools folder exists
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
$ToolPath = Join-Path $PSScriptRoot "tools"
if (!(Test-Path $ToolPath)) {
    Write-Verbose "Creating tools directory..."
    New-Item -Path $ToolPath -Type directory | out-null
}

###########################################################################
# INSTALL NUGET
###########################################################################

# Make sure nuget.exe exists.
$NugetPath = Join-Path $ToolPath "nuget.exe"
if (!(Test-Path $NugetPath)) {
    Write-Host "Downloading NuGet.exe..."
    (New-Object System.Net.WebClient).DownloadFile($NugetUrl, $NugetPath);
}

###########################################################################
# INSTALL FAKE
###########################################################################
# Make sure Fake has been installed.

$FakeExePath = Join-Path $ToolPath "FAKE/tools/FAKE.exe"
if (!(Test-Path $FakeExePath)) {
    Write-Host "Installing Fake..."
    Invoke-Expression "&`"$NugetPath`" install Fake -ExcludeVersion -Version $FakeVersion -OutputDirectory `"$ToolPath`"" | Out-Null;
    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring Fake from NuGet."
    }
}

###########################################################################
# Docfx
###########################################################################

# Make sure Docfx has been installed.
$DocfxExePath = Join-Path $ToolPath "docfx.console/tools/docfx.exe"
if (!(Test-Path $DocfxExePath)) {
    Write-Host "Installing Docfx..."
    Invoke-Expression "&`"$NugetPath`" install docfx.console -ExcludeVersion -Version $DocfxVersion -OutputDirectory `"$ToolPath`"" | Out-Null;
    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring docfx.console from NuGet."
    }
}

###########################################################################
# SignTool
###########################################################################

# Make sure the SignClient has been installed
if (Get-Command signclient -ErrorAction SilentlyContinue) {
    Write-Host "Found SignClient. Skipping install."
}
else{
    $SignClientFolder = Join-Path $ToolPath "signclient"
    Write-Host "SignClient not found. Installing to ... $SignClientFolder"
    dotnet tool install SignClient --version 1.0.82 --tool-path "$SignClientFolder"
}

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

# Build the argument list.
$Arguments = @{
    target=$Target;
    configuration=$Configuration;
    verbosity=$Verbosity;
    dryrun=$WhatIf;
}.GetEnumerator() | %{"--{0}=`"{1}`"" -f $_.key, $_.value };

# Start Fake
Write-Host "Running build script..."
Invoke-Expression "$FakeExePath `"build.fsx`" $ScriptArgs $Arguments"

exit $LASTEXITCODE
# SIG # Begin signature block
# MIIewgYJKoZIhvcNAQcCoIIeszCCHq8CAQExDzANBglghkgBZQMEAgEFADB5Bgor
# BgEEAYI3AgEEoGswaTA0BgorBgEEAYI3AgEeMCYCAwEAAAQQH8w7YFlLCE63JNLG
# KX7zUQIBAAIBAAIBAAIBAAIBADAxMA0GCWCGSAFlAwQCAQUABCD7aT1U4TM55/A0
# WgIhzskl7thEawtoIE68ZuiF8lMKNqCCDhkwggO3MIICn6ADAgECAhAM5+DlF9hG
# /o/lYPwb8DA5MA0GCSqGSIb3DQEBBQUAMGUxCzAJBgNVBAYTAlVTMRUwEwYDVQQK
# EwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5jb20xJDAiBgNV
# BAMTG0RpZ2lDZXJ0IEFzc3VyZWQgSUQgUm9vdCBDQTAeFw0wNjExMTAwMDAwMDBa
# Fw0zMTExMTAwMDAwMDBaMGUxCzAJBgNVBAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2Vy
# dCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5jb20xJDAiBgNVBAMTG0RpZ2lD
# ZXJ0IEFzc3VyZWQgSUQgUm9vdCBDQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCC
# AQoCggEBAK0OFc7kQ4BcsYfzt2D5cRKlrtwmlIiq9M71IDkoWGAM+IDaqRWVMmE8
# tbEohIqK3J8KDIMXeo+QrIrneVNcMYQq9g+YMjZ2zN7dPKii72r7IfJSYd+fINcf
# 4rHZ/hhk0hJbX/lYGDW8R82hNvlrf9SwOD7BG8OMM9nYLxj+KA+zp4PWw25EwGE1
# lhb+WZyLdm3X8aJLDSv/C3LanmDQjpA1xnhVhyChz+VtCshJfDGYM2wi6YfQMlqi
# uhOCEe05F52ZOnKh5vqk2dUXMXWuhX0irj8BRob2KHnIsdrkVxfEfhwOsLSSplaz
# vbKX7aqn8LfFqD+VFtD/oZbrCF8Yd08CAwEAAaNjMGEwDgYDVR0PAQH/BAQDAgGG
# MA8GA1UdEwEB/wQFMAMBAf8wHQYDVR0OBBYEFEXroq/0ksuCMS1Ri6enIZ3zbcgP
# MB8GA1UdIwQYMBaAFEXroq/0ksuCMS1Ri6enIZ3zbcgPMA0GCSqGSIb3DQEBBQUA
# A4IBAQCiDrzf4u3w43JzemSUv/dyZtgy5EJ1Yq6H6/LV2d5Ws5/MzhQouQ2XYFwS
# TFjk0z2DSUVYlzVpGqhH6lbGeasS2GeBhN9/CTyU5rgmLCC9PbMoifdf/yLil4Qf
# 6WXvh+DfwWdJs13rsgkq6ybteL59PyvztyY1bV+JAbZJW58BBZurPSXBzLZ/wvFv
# hsb6ZGjrgS2U60K3+owe3WLxvlBnt2y98/Efaww2BxZ/N3ypW2168RJGYIPXJwS+
# S86XvsNnKmgR34DnDDNmvxMNFG7zfx9jEB76jRslbWyPpbdhAbHSoyahEHGdreLD
# +cOZUbcrBwjOLuZQsqf6CkUvovDyMIIFJjCCBA6gAwIBAgIQBcC4m7IhMrtdg5wV
# IYUHmzANBgkqhkiG9w0BAQsFADByMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGln
# aUNlcnQgSW5jMRkwFwYDVQQLExB3d3cuZGlnaWNlcnQuY29tMTEwLwYDVQQDEyhE
# aWdpQ2VydCBTSEEyIEFzc3VyZWQgSUQgQ29kZSBTaWduaW5nIENBMB4XDTIwMDEy
# OTAwMDAwMFoXDTIzMDEzMTEyMDAwMFowYzELMAkGA1UEBhMCVVMxDjAMBgNVBAgT
# BVRleGFzMRAwDgYDVQQHEwdIb3VzdG9uMRgwFgYDVQQKEw9QZXRhYnJpZGdlLCBM
# TEMxGDAWBgNVBAMTD1BldGFicmlkZ2UsIExMQzCCASIwDQYJKoZIhvcNAQEBBQAD
# ggEPADCCAQoCggEBAL0BBOw8B5zHcjHQ/qcF1OrQv7Z1U5NiuEtqikJnkK9OhMzx
# k0Hn9TOReZkHHe1oV2Ouf43E9RpvfufijJIUKpFMezN6tXTyTyMFbgr+sASxaGf3
# RxjyH2caP1ozTtLyycOFR7DKI3tbNhTTAtQkkTvgQ6buDjEhFTBhn5kI8AoXelXb
# n92uFRn7FOGOPno/Lru85Sg5QLz9iqHx4Vje7JeEzQbnB5K6mntZuheSIdnpDCIq
# xPfLtackHqvAKxoYXg8hCP+LTsgRaxNbu8Wi6pDfYjqEeLhBqHZda5T/TF887rHQ
# tgE9XyBOUokrWW2b2+29vCVkTaFTmMauJP5GU7kCAwEAAaOCAcUwggHBMB8GA1Ud
# IwQYMBaAFFrEuXsqCqOl6nEDwGD5LfZldQ5YMB0GA1UdDgQWBBQP0wazew0SBdcA
# 0LguXFA9pT8ZrTAOBgNVHQ8BAf8EBAMCB4AwEwYDVR0lBAwwCgYIKwYBBQUHAwMw
# dwYDVR0fBHAwbjA1oDOgMYYvaHR0cDovL2NybDMuZGlnaWNlcnQuY29tL3NoYTIt
# YXNzdXJlZC1jcy1nMS5jcmwwNaAzoDGGL2h0dHA6Ly9jcmw0LmRpZ2ljZXJ0LmNv
# bS9zaGEyLWFzc3VyZWQtY3MtZzEuY3JsMEwGA1UdIARFMEMwNwYJYIZIAYb9bAMB
# MCowKAYIKwYBBQUHAgEWHGh0dHBzOi8vd3d3LmRpZ2ljZXJ0LmNvbS9DUFMwCAYG
# Z4EMAQQBMIGEBggrBgEFBQcBAQR4MHYwJAYIKwYBBQUHMAGGGGh0dHA6Ly9vY3Nw
# LmRpZ2ljZXJ0LmNvbTBOBggrBgEFBQcwAoZCaHR0cDovL2NhY2VydHMuZGlnaWNl
# cnQuY29tL0RpZ2lDZXJ0U0hBMkFzc3VyZWRJRENvZGVTaWduaW5nQ0EuY3J0MAwG
# A1UdEwEB/wQCMAAwDQYJKoZIhvcNAQELBQADggEBADgE3rY8X9MYn4tEPaGxRkzH
# IzTGb236WdbwqDXIiZfjIC4h3/FMOhvSeC63blYti9ExgEg2en3UmmNbLxUgbQU8
# HK9giaMwqPCWPH+vKkdXtzTFQMkNQPkboOlVU4+2WtLHkrIQwJ3hOEveKAmNuyST
# yWCQD6SNQu0o6GAEKyg4HBVapOf0ioAS0T6ijnT/Sgh98DJSEqzslZ3/RWps51nw
# W3UedbvzCd/xpQx/5IT/hIvK8pJ2IsSW2OKAh450ZWR2LKY9oODZmfFpUvDiZBYs
# RQWRUUvcNDb5mAln3snmVbe1gIcekV7iG7iwzx54K3MFL1OjGKVR5JZcfpNCz4Uw
# ggUwMIIEGKADAgECAhAECRgbX9W7ZnVTQ7VvlVAIMA0GCSqGSIb3DQEBCwUAMGUx
# CzAJBgNVBAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3
# dy5kaWdpY2VydC5jb20xJDAiBgNVBAMTG0RpZ2lDZXJ0IEFzc3VyZWQgSUQgUm9v
# dCBDQTAeFw0xMzEwMjIxMjAwMDBaFw0yODEwMjIxMjAwMDBaMHIxCzAJBgNVBAYT
# AlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2Vy
# dC5jb20xMTAvBgNVBAMTKERpZ2lDZXJ0IFNIQTIgQXNzdXJlZCBJRCBDb2RlIFNp
# Z25pbmcgQ0EwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQD407Mcfw4R
# r2d3B9MLMUkZz9D7RZmxOttE9X/lqJ3bMtdx6nadBS63j/qSQ8Cl+YnUNxnXtqrw
# nIal2CWsDnkoOn7p0WfTxvspJ8fTeyOU5JEjlpB3gvmhhCNmElQzUHSxKCa7JGnC
# wlLyFGeKiUXULaGj6YgsIJWuHEqHCN8M9eJNYBi+qsSyrnAxZjNxPqxwoqvOf+l8
# y5Kh5TsxHM/q8grkV7tKtel05iv+bMt+dDk2DZDv5LVOpKnqagqrhPOsZ061xPeM
# 0SAlI+sIZD5SlsHyDxL0xY4PwaLoLFH3c7y9hbFig3NBggfkOItqcyDQD2RzPJ6f
# pjOp/RnfJZPRAgMBAAGjggHNMIIByTASBgNVHRMBAf8ECDAGAQH/AgEAMA4GA1Ud
# DwEB/wQEAwIBhjATBgNVHSUEDDAKBggrBgEFBQcDAzB5BggrBgEFBQcBAQRtMGsw
# JAYIKwYBBQUHMAGGGGh0dHA6Ly9vY3NwLmRpZ2ljZXJ0LmNvbTBDBggrBgEFBQcw
# AoY3aHR0cDovL2NhY2VydHMuZGlnaWNlcnQuY29tL0RpZ2lDZXJ0QXNzdXJlZElE
# Um9vdENBLmNydDCBgQYDVR0fBHoweDA6oDigNoY0aHR0cDovL2NybDQuZGlnaWNl
# cnQuY29tL0RpZ2lDZXJ0QXNzdXJlZElEUm9vdENBLmNybDA6oDigNoY0aHR0cDov
# L2NybDMuZGlnaWNlcnQuY29tL0RpZ2lDZXJ0QXNzdXJlZElEUm9vdENBLmNybDBP
# BgNVHSAESDBGMDgGCmCGSAGG/WwAAgQwKjAoBggrBgEFBQcCARYcaHR0cHM6Ly93
# d3cuZGlnaWNlcnQuY29tL0NQUzAKBghghkgBhv1sAzAdBgNVHQ4EFgQUWsS5eyoK
# o6XqcQPAYPkt9mV1DlgwHwYDVR0jBBgwFoAUReuir/SSy4IxLVGLp6chnfNtyA8w
# DQYJKoZIhvcNAQELBQADggEBAD7sDVoks/Mi0RXILHwlKXaoHV0cLToaxO8wYdd+
# C2D9wz0PxK+L/e8q3yBVN7Dh9tGSdQ9RtG6ljlriXiSBThCk7j9xjmMOE0ut119E
# efM2FAaK95xGTlz/kLEbBw6RFfu6r7VRwo0kriTGxycqoSkoGjpxKAI8LpGjwCUR
# 4pwUR6F6aGivm6dcIFzZcbEMj7uo+MUSaJ/PQMtARKUT8OZkDCUIQjKyNookAv4v
# cn4c10lFluhZHen6dGRrsutmQ9qzsIzV6Q3d9gEgzpkxYz0IGhizgZtPxpMQBvwH
# gfqL2vmCSfdibqFT+hKUGIUukpHqaGxEMrJmoecYpJpkUe8xgg//MIIP+wIBATCB
# hjByMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQL
# ExB3d3cuZGlnaWNlcnQuY29tMTEwLwYDVQQDEyhEaWdpQ2VydCBTSEEyIEFzc3Vy
# ZWQgSUQgQ29kZSBTaWduaW5nIENBAhAFwLibsiEyu12DnBUhhQebMA0GCWCGSAFl
# AwQCAQUAoIIBATAZBgkqhkiG9w0BCQMxDAYKKwYBBAGCNwIBBDAcBgorBgEEAYI3
# AgELMQ4wDAYKKwYBBAGCNwIBFTAvBgkqhkiG9w0BCQQxIgQgWgKTduRZjzKRniiP
# hE46NcaFiZkupqqveLKbJRQZhxQwgZQGCisGAQQBgjcCAQwxgYUwgYKgSIBGAFAA
# ZQB0AGEAYgByAGkAZABnAGUAIABTAHQAYQBuAGQAYQByAGQAIABCAHUAaQBsAGQA
# IABUAGUAbQBwAGwAYQB0AGUAc6E2gDRodHRwczovL2dpdGh1Yi5jb20vcGV0YWJy
# aWRnZS9wZXRhYnJpZGdlLWRvdG5ldC1uZXcgMA0GCSqGSIb3DQEBAQUABIIBALis
# uNHGVGTNpZ0l+1D4RTAHhEzBQYKxGzTMaDIrfu8xpYQjwqcONgYixa3XGOHQpN27
# nHkWf3l5PJeIyTPhHxnTJiR9LBoooOEDDslFQaYJaJw+FTgZNFFN/k9kcFDZfR4d
# aIATbNANNOxg1ldpctKrzDU68A+5/yp26LWiTY210uL4hh0ROXCIuy+n82Gn8Q3s
# 5aMl3pggnf5KSgkK3BDXmTzIFviPqaserGQFWbOENuWw3HQUqMnT66SdAM73E/AI
# S3rNnPVCinkj2yoczFCgWVVJG0Q+5o+0CJ1EHg0CN6YHDp60RBzkOBsGnhUId7/o
# GhjGHXsJd96+ZadhffKhgg1EMIINQAYKKwYBBAGCNwMDATGCDTAwgg0sBgkqhkiG
# 9w0BBwKggg0dMIINGQIBAzEPMA0GCWCGSAFlAwQCAQUAMHcGCyqGSIb3DQEJEAEE
# oGgEZjBkAgEBBglghkgBhv1sBwEwMTANBglghkgBZQMEAgEFAAQgxYZ4fcaFT2+I
# HKWF2LujX9YsP+BNFTBIxFYWYFs+S58CEEKqStwMFYMMMOc8n+L8MCkYDzIwMjEw
# NDE1MTUwMDMyWqCCCjcwggT+MIID5qADAgECAhANQkrgvjqI/2BAIc4UAPDdMA0G
# CSqGSIb3DQEBCwUAMHIxCzAJBgNVBAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJ
# bmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5jb20xMTAvBgNVBAMTKERpZ2lDZXJ0
# IFNIQTIgQXNzdXJlZCBJRCBUaW1lc3RhbXBpbmcgQ0EwHhcNMjEwMTAxMDAwMDAw
# WhcNMzEwMTA2MDAwMDAwWjBIMQswCQYDVQQGEwJVUzEXMBUGA1UEChMORGlnaUNl
# cnQsIEluYy4xIDAeBgNVBAMTF0RpZ2lDZXJ0IFRpbWVzdGFtcCAyMDIxMIIBIjAN
# BgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwuZhhGfFivUNCKRFymNrUdc6EUK9
# CnV1TZS0DFC1JhD+HchvkWsMlucaXEjvROW/m2HNFZFiWrj/ZwucY/02aoH6Kfjd
# K3CF3gIY83htvH35x20JPb5qdofpir34hF0edsnkxnZ2OlPR0dNaNo/Go+EvGzq3
# YdZz7E5tM4p8XUUtS7FQ5kE6N1aG3JMjjfdQJehk5t3Tjy9XtYcg6w6OLNUj2vRN
# eEbjA4MxKUpcDDGKSoyIxfcwWvkUrxVfbENJCf0mI1P2jWPoGqtbsR0wwptpgrTb
# /FZUvB+hh6u+elsKIC9LCcmVp42y+tZji06lchzun3oBc/gZ1v4NSYS9AQIDAQAB
# o4IBuDCCAbQwDgYDVR0PAQH/BAQDAgeAMAwGA1UdEwEB/wQCMAAwFgYDVR0lAQH/
# BAwwCgYIKwYBBQUHAwgwQQYDVR0gBDowODA2BglghkgBhv1sBwEwKTAnBggrBgEF
# BQcCARYbaHR0cDovL3d3dy5kaWdpY2VydC5jb20vQ1BTMB8GA1UdIwQYMBaAFPS2
# 4SAd/imu0uRhpbKiJbLIFzVuMB0GA1UdDgQWBBQ2RIaOpLqwZr68KC0dRDbd42p6
# vDBxBgNVHR8EajBoMDKgMKAuhixodHRwOi8vY3JsMy5kaWdpY2VydC5jb20vc2hh
# Mi1hc3N1cmVkLXRzLmNybDAyoDCgLoYsaHR0cDovL2NybDQuZGlnaWNlcnQuY29t
# L3NoYTItYXNzdXJlZC10cy5jcmwwgYUGCCsGAQUFBwEBBHkwdzAkBggrBgEFBQcw
# AYYYaHR0cDovL29jc3AuZGlnaWNlcnQuY29tME8GCCsGAQUFBzAChkNodHRwOi8v
# Y2FjZXJ0cy5kaWdpY2VydC5jb20vRGlnaUNlcnRTSEEyQXNzdXJlZElEVGltZXN0
# YW1waW5nQ0EuY3J0MA0GCSqGSIb3DQEBCwUAA4IBAQBIHNy16ZojvOca5yAOjmdG
# /UJyUXQKI0ejq5LSJcRwWb4UoOUngaVNFBUZB3nw0QTDhtk7vf5EAmZN7WmkD/a4
# cM9i6PVRSnh5Nnont/PnUp+Tp+1DnnvntN1BIon7h6JGA0789P63ZHdjXyNSaYOC
# +hpT7ZDMjaEXcw3082U5cEvznNZ6e9oMvD0y0BvL9WH8dQgAdryBDvjA4VzPxBFy
# 5xtkSdgimnUVQvUtMjiB2vRgorq0Uvtc4GEkJU+y38kpqHNDUdq9Y9YfW5v3LhtP
# Ex33Sg1xfpe39D+E68Hjo0mh+s6nv1bPull2YYlffqe0jmd4+TaY4cso2luHpoov
# MIIFMTCCBBmgAwIBAgIQCqEl1tYyG35B5AXaNpfCFTANBgkqhkiG9w0BAQsFADBl
# MQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3
# d3cuZGlnaWNlcnQuY29tMSQwIgYDVQQDExtEaWdpQ2VydCBBc3N1cmVkIElEIFJv
# b3QgQ0EwHhcNMTYwMTA3MTIwMDAwWhcNMzEwMTA3MTIwMDAwWjByMQswCQYDVQQG
# EwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3d3cuZGlnaWNl
# cnQuY29tMTEwLwYDVQQDEyhEaWdpQ2VydCBTSEEyIEFzc3VyZWQgSUQgVGltZXN0
# YW1waW5nIENBMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAvdAy7kvN
# j3/dqbqCmcU5VChXtiNKxA4HRTNREH3Q+X1NaH7ntqD0jbOI5Je/YyGQmL8TvFfT
# w+F+CNZqFAA49y4eO+7MpvYyWf5fZT/gm+vjRkcGGlV+Cyd+wKL1oODeIj8O/36V
# +/OjuiI+GKwR5PCZA207hXwJ0+5dyJoLVOOoCXFr4M8iEA91z3FyTgqt30A6XLdR
# 4aF5FMZNJCMwXbzsPGBqrC8HzP3w6kfZiFBe/WZuVmEnKYmEUeaC50ZQ/ZQqLKfk
# dT66mA+Ef58xFNat1fJky3seBdCEGXIX8RcG7z3N1k3vBkL9olMqT4UdxB08r8/a
# rBD13ays6Vb/kwIDAQABo4IBzjCCAcowHQYDVR0OBBYEFPS24SAd/imu0uRhpbKi
# JbLIFzVuMB8GA1UdIwQYMBaAFEXroq/0ksuCMS1Ri6enIZ3zbcgPMBIGA1UdEwEB
# /wQIMAYBAf8CAQAwDgYDVR0PAQH/BAQDAgGGMBMGA1UdJQQMMAoGCCsGAQUFBwMI
# MHkGCCsGAQUFBwEBBG0wazAkBggrBgEFBQcwAYYYaHR0cDovL29jc3AuZGlnaWNl
# cnQuY29tMEMGCCsGAQUFBzAChjdodHRwOi8vY2FjZXJ0cy5kaWdpY2VydC5jb20v
# RGlnaUNlcnRBc3N1cmVkSURSb290Q0EuY3J0MIGBBgNVHR8EejB4MDqgOKA2hjRo
# dHRwOi8vY3JsNC5kaWdpY2VydC5jb20vRGlnaUNlcnRBc3N1cmVkSURSb290Q0Eu
# Y3JsMDqgOKA2hjRodHRwOi8vY3JsMy5kaWdpY2VydC5jb20vRGlnaUNlcnRBc3N1
# cmVkSURSb290Q0EuY3JsMFAGA1UdIARJMEcwOAYKYIZIAYb9bAACBDAqMCgGCCsG
# AQUFBwIBFhxodHRwczovL3d3dy5kaWdpY2VydC5jb20vQ1BTMAsGCWCGSAGG/WwH
# ATANBgkqhkiG9w0BAQsFAAOCAQEAcZUS6VGHVmnN793afKpjerN4zwY3QITvS4S/
# ys8DAv3Fp8MOIEIsr3fzKx8MIVoqtwU0HWqumfgnoma/Capg33akOpMP+LLR2HwZ
# YuhegiUexLoceywh4tZbLBQ1QwRostt1AuByx5jWPGTlH0gQGF+JOGFNYkYkh2OM
# kVIsrymJ5Xgf1gsUpYDXEkdws3XVk4WTfraSZ/tTYYmo9WuWwPRYaQ18yAGxuSh1
# t5ljhSKMYcp5lH5Z/IwP42+1ASa2bKXuh1Eh5Fhgm7oMLSttosR+u8QlK0cCCHxJ
# rhO24XxCQijGGFbPQTS2Zl22dHv1VjMiLyI2skuiSpXY9aaOUjGCAk0wggJJAgEB
# MIGGMHIxCzAJBgNVBAYTAlVTMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNV
# BAsTEHd3dy5kaWdpY2VydC5jb20xMTAvBgNVBAMTKERpZ2lDZXJ0IFNIQTIgQXNz
# dXJlZCBJRCBUaW1lc3RhbXBpbmcgQ0ECEA1CSuC+Ooj/YEAhzhQA8N0wDQYJYIZI
# AWUDBAIBBQCggZgwGgYJKoZIhvcNAQkDMQ0GCyqGSIb3DQEJEAEEMBwGCSqGSIb3
# DQEJBTEPFw0yMTA0MTUxNTAwMzJaMCsGCyqGSIb3DQEJEAIMMRwwGjAYMBYEFOHX
# gqjhkb7va8oWkbWqtJSmJJvzMC8GCSqGSIb3DQEJBDEiBCDr3RR75SQ0CYrWaYN6
# H/LjgdaV3x/dMtySRhppuaLkXjANBgkqhkiG9w0BAQEFAASCAQCK+rAwCQsCrYNB
# 2GzN+cUTmcizXNRFd6eZFabDWbIKoND/Po2dGXCVfWQhFqI+bT8CZMfshi7ToUP3
# VL+eBZKOCILvdQfdF6ehNHLXkPP/J70gaA39VzUUKLJ9WAHt0ypETUni9zRX7GUO
# lrRKgduFk1dDrqRvo5uSluN6sMz1qJPMp2HRLVMQQHq/wQ+O4FUY6RlhWkCi8MWu
# SbJUCUUERAngluHmLOiL7TPq0Z50xwKCUOk5wNfZJDbpV/UnPFkj7fAQ0ygueq/6
# ErhHqE819qJtBMSju6Clc1/Xpb22kSBN53/eGcN586+L925EDfg6je9pow8xDYmq
# dZUz8bTQ
# SIG # End signature block

param(
    [string] $ApplicationVersion,
    [string] $ApplicationDisplayVersion,
    [Parameter(Mandatory = $true)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'

if ($ApplicationVersion -and $ApplicationVersion -notmatch '^[1-9][0-9]*$') {
    throw 'The iOS CFBundleVersion must be a positive integer.'
}

if ($ApplicationDisplayVersion -and $ApplicationDisplayVersion -notmatch '^[0-9]+(\.[0-9]+){0,2}$') {
    throw 'The iOS CFBundleShortVersionString must be one to three period-separated integers, for example 1.0.'
}

$requiredSecrets = @(
    'IOS_DISTRIBUTION_CERTIFICATE_P12_BASE64',
    'IOS_DISTRIBUTION_CERTIFICATE_PASSWORD',
    'IOS_PROVISIONING_PROFILE_BASE64'
)
$missingSecrets = $requiredSecrets | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_))
}
if ($missingSecrets.Count -gt 0) {
    throw "Missing required GitHub secrets: $($missingSecrets -join ', '). See docs/ios-testflight-internal-testing.md."
}

$certificatePath = Join-Path $env:RUNNER_TEMP 'ios-distribution.p12'
$profilePath = Join-Path $env:RUNNER_TEMP 'ios-distribution.mobileprovision'
$profilePlistPath = Join-Path $env:RUNNER_TEMP 'ios-distribution-profile.plist'
$keychainPath = Join-Path $env:RUNNER_TEMP 'catch-ios-signing.keychain-db'
$keychainPassword = [Guid]::NewGuid().ToString('N')
$publishDirectory = Join-Path $env:RUNNER_TEMP "catch-ios-publish-$([Guid]::NewGuid().ToString('N'))"
$createdKeychain = $false

function ConvertFrom-Base64File {
    param(
        [Parameter(Mandatory = $true)]
        [string] $EnvironmentName,
        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    $bytes = [Convert]::FromBase64String([Environment]::GetEnvironmentVariable($EnvironmentName))
    if ($bytes.Length -eq 0) {
        throw "$EnvironmentName decoded to an empty file."
    }

    [IO.File]::WriteAllBytes($Destination, $bytes)
}

try {
    ConvertFrom-Base64File -EnvironmentName 'IOS_DISTRIBUTION_CERTIFICATE_P12_BASE64' -Destination $certificatePath
    ConvertFrom-Base64File -EnvironmentName 'IOS_PROVISIONING_PROFILE_BASE64' -Destination $profilePath

    & security create-keychain -p $keychainPassword $keychainPath
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not create the temporary iOS signing keychain.'
    }

    $createdKeychain = $true
    & security set-keychain-settings -lut 21600 $keychainPath
    & security unlock-keychain -p $keychainPassword $keychainPath
    & security import $certificatePath -k $keychainPath -P $env:IOS_DISTRIBUTION_CERTIFICATE_PASSWORD -T /usr/bin/codesign -T /usr/bin/security -A
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not import the Apple Distribution certificate into the temporary keychain.'
    }

    & security set-key-partition-list -S 'apple-tool:,apple:,codesign:' -s -k $keychainPassword $keychainPath | Out-Null
    $existingKeychains = @(& security list-keychains -d user | ForEach-Object { $_.Trim().Trim('"') })
    & security list-keychains -d user -s $keychainPath @existingKeychains | Out-Null

    $identityOutput = & security find-identity -v -p codesigning $keychainPath | Out-String
    if ($identityOutput -notmatch '([0-9A-F]{40}).*Apple Distribution') {
        throw 'The imported certificate did not contain an Apple Distribution codesigning identity.'
    }

    $codesignKey = $Matches[1]
    & security cms -D -i $profilePath -o $profilePlistPath
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not decode the App Store provisioning profile.'
    }

    $profileName = (& /usr/libexec/PlistBuddy -c 'Print :Name' $profilePlistPath).Trim()
    $profileUuid = (& /usr/libexec/PlistBuddy -c 'Print :UUID' $profilePlistPath).Trim()
    if ([string]::IsNullOrWhiteSpace($profileName) -or [string]::IsNullOrWhiteSpace($profileUuid)) {
        throw 'The App Store provisioning profile did not include a Name and UUID.'
    }

    $profilesDirectory = Join-Path $HOME 'Library/MobileDevice/Provisioning Profiles'
    New-Item -ItemType Directory -Force -Path $profilesDirectory | Out-Null
    Copy-Item -LiteralPath $profilePath -Destination (Join-Path $profilesDirectory "$profileUuid.mobileprovision") -Force

    $properties = @(
        '-f', 'net10.0-ios',
        '-c', 'Release',
        '-p:RuntimeIdentifier=ios-arm64',
        '-p:MobileEnvironment=Development',
        '-p:ArchiveOnBuild=true',
        '-p:EnableCodeSigning=true',
        "-p:CodesignKey=$codesignKey",
        "-p:CodesignProvision=$profileName"
    )
    if ($ApplicationVersion) {
        $properties += "-p:ApplicationVersion=$ApplicationVersion"
    }
    if ($ApplicationDisplayVersion) {
        $properties += "-p:ApplicationDisplayVersion=$ApplicationDisplayVersion"
    }

    dotnet publish src/FishingLogBook.Mobile/FishingLogBook.Mobile.csproj @properties --output $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    node .github/scripts/resolve-signed-ios-ipa.mjs $publishDirectory $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "Signed IPA resolution failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($createdKeychain -and (Test-Path -LiteralPath $keychainPath)) {
        & security delete-keychain $keychainPath | Out-Null
    }

    foreach ($path in @($certificatePath, $profilePath, $profilePlistPath)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Force
        }
    }
}

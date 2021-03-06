#
# Terraria Library Publishing Script, CoderCow 2017
#
# 
# This script performs the following actions:
#   * Uses GitVersion to increment a component of the version number in the project's AssemblyInfo.cs.
#     The component of the SemVer version number to increase (major, minor or patch) is determined by 
#     the commits done since the last tag (BREAKING, feature or fix commits).
#   * Uses clog to generate a changelog in markdown format. The entries are also determined by the 
#     commits since the last tag.
#   * Bundles release files in a zip file.
#   * Creates a commit for the changed AssemblyInfo.cs and tags this commit with the new SemVer,
#     also pushes this commit and the tag.
#   * Uses GitReleaseManager to create a GitHub release and attaches the zip file to it, using the
#     generated changelog as the description text.
#
# Usage Notes
#
#   The conventional commit message format of angularjs is expected when scanning recent commits. 
#   It's documented here: https://github.com/conventional-changelog/conventional-changelog/blob/a5505865ff3dd710cf757f50530e73ef0ca641da/conventions/angular.md
#
#   Do not run this script when you're currently on a tagged commit. 
#   Do not commit AssemblyInfo.cs before invoking this script.
#
#   If there were neither major, minor nor patch commits the patch component of the version number will
#   still be increased when you execute this script.
#
#   To create pre-releases, make a new branch and call it "pre". When running this script while being on
#   a commit in this branch, versions will looks like this "X.X.X-pre.Y" where X.X.X is your next release
#   version and Y your current pre-release candidate. The version should become "X.X.X" (without the suffix) 
#   once you merge "pre" (fast forward should work fine) back into master and run this script again.
#
# Installing Dependencies
#
#   If you have chocolatey, you can install all of the required dependencies with:
#     choco install GitReleaseManager.Portable GitVersion.Portable 7zip rust -y & cargo install clog-cli
# 
#   Note that cargo is the bundled package manager of rust.
#   You may have to register some of the binaries in your PATH.
#
# Further Notes
# 
#   In case you prefer GitHub issues for the changelog generation instead of commits you might rewrite 
#   this script to use GitReleaseManager for this, as it supports changelogs based on issues and milestones.
#
#   clog repo: https://github.com/clog-tool/clog-cli
#   GitVersion docs: http://gitversion.readthedocs.io
#   GitReleaseManager docs: http://gitreleasemanager.readthedocs.io

$ErrorActionPreference = "Stop"

$outDir = "$PSScriptRoot\bin\Release"
$assemblyInfoPath = "$PSScriptRoot\Properties\AssemblyInfo.cs"
# the tshock binary is used to determine the tshock version this library was built against
$tshockBinaryPath = "$outDir\TShockAPI.dll"
# the OTAPI binary is used to determine the Terraria version this library was built against
$otapiBinaryPath = "$outDir\OTAPI.dll"
$targetName = "Plugin Common Library"
$projectFile = "$PSScriptRoot\$targetName.csproj"
$commitMessageFormat = "chore(version): tick version {0}"
$tagNameFormat = "release {0}, Terraria {1}"
$outZipFileNameFormat = "CommonLib_{0}_Terraria_{1}.zip"

$gitHubUser = "CoderCow"
$gitHubRepoOwner = "CoderCow"
$gitHubRepoName = "PluginCommonLibrary"

$binariesToPublish = @(
  "$outDir\Plugin Common Lib *.dll",
  "$outDir\Plugin Common Lib *.pdb"
)

function Main {
  $tshockVersion = Get-TshockVersion
  Write-Host "This library was built against TShock $tshockVersion" -ForegroundColor Cyan

  $terrariaVersion = Get-OtapiVersion
  Write-Host "This library was built against Terraria $terrariaVersion" -ForegroundColor Cyan

  $versionInfo = Update-AssemblyVersion
  $releaseVersion = $versionInfo.SemVer
  $isPrerelease = $versionInfo.PreReleaseTag -ne ""
  Write-Host "Release version will be $releaseVersion"

  $outChangelogFile = "$outDir\changelog.md"
  $gitHubUrl = "https://github.com/$gitHubUser/$gitHubRepoName"
  Generate-Changelog $tshockVersion $terrariaVersion $outChangelogFile $gitHubUrl

  $outZipFile = "$outDir\" + ($outZipFileNameFormat -f $releaseVersion,$terrariaVersion)
  Package-Files $outZipFile

  Create-Commit $releaseVersion $terrariaVersion

  Create-GitHubRelease $releaseVersion $outChangelogFile $outZipFile
  Start-Process "$gitHubUrl/releases"
}

function Get-TShockVersion {
  # Get the file version of the TShock binary
  [System.Diagnostics.FileVersionInfo]::GetVersionInfo($tshockBinaryPath).FileVersion.ToString()
}

function Get-OtapiVersion {
  # Get the file version of the OTAPI binary
  [System.Diagnostics.FileVersionInfo]::GetVersionInfo($otapiBinaryPath).FileVersion.ToString()
}

function Update-AssemblyVersion {
  # GitVersion will increment the assembly version and return some information about it in json format
  # Actually, this should have happened already be the pre-build event configured for the project, so this
  # additional call to GitVersion will just return the current version number.
  GitVersion.exe /updateassemblyinfo $assemblyInfoPath | ConvertFrom-Json
}

function Generate-Changelog($tshockVersion, $terrariaVersion, $outChangelogFile, $gitHubUrl) {
  if (Test-Path $outChangelogFile) {
    Remove-Item -Force $outChangelogFile
  }

  # clog builds a markdown changelog from all commits since the last tag
  clog.exe --from-latest-tag --setversion $releaseVersion --outfile $outChangelogFile --repository $gitHubUrl

  # add some custom lines to the changelog
  if ($isPrerelease) {
    Add-Content "$outChangelogFile" "**NOTE: This is a pre-release currently under test.**`n"
  }
  Add-Content "$outChangelogFile" "This library was built against Terraria **$terrariaVersion** and TShock **$tshockVersion**."
  
  # print the changelog for validation
  Write-Host "---- Content of $outChangelogFile ----" -ForegroundColor Green
  Get-Content $outChangelogFile | Write-Host -ForegroundColor Cyan
  Write-Host "------------- EOF -------------" -ForegroundColor Green

  $wantToEdit = Read-Host "Do you want to edit the changelog? [y/n]"
  if ($wantToEdit -eq "y") {
    Start-Process $outChangelogFile
    Read-Host "Press any key to continue"
  }
}

function Package-Files($outZipFile) {
  if (Test-Path $outZipFile) {
    Remove-Item -Force $outZipFile
  }

  7z.exe a -y -r -bd -tzip -mx9 $outZipFile $binariesToPublish > $null
}

function Create-Commit($releaseVersion, $terrariaVersion) {
  $tagName = $tagNameFormat -f $releaseVersion,$terrariaVersion
  $commitMessage = $commitMessageFormat -f $releaseVersion,$terrariaVersion

  git add $assemblyInfoPath
  git commit --message $commitMessage
  git tag --annotate $releaseVersion --message $tagName
}

function Create-GitHubRelease($releaseVersion, $outChangelogFile, $outZipFile) {
  $gitHubPassword = Read-Host "Enter password for GitHub user $gitHubUser"

  # This ensures that errors can be seen if they happen
  $ErrorActionPreference = "Continue"

  git push origin --follow-tags
  GitReleaseManager.exe create -u $gitHubUser -p $gitHubPassword -o $gitHubRepoOwner -r $gitHubRepoName -n $releaseVersion -i $outChangelogFile -a $outZipFile
  GitReleaseManager.exe publish -u $gitHubUser -p $gitHubPassword -o $gitHubRepoOwner -r $gitHubRepoName -t $releaseVersion
}

Main
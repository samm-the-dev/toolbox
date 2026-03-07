param(
    [string]$Title = "Claude Code",
    [string]$Message = "Needs your attention",
    [string]$Cwd = ""
)

# Requires BurntToast module:
#   Install-Module -Name BurntToast -Scope CurrentUser -Force
#
# Optional: set $iconPath to a local PNG for a custom icon.
# Download any image and update the path below.
$iconPath = ""  # e.g. "C:\Dev\claude-icon.png"

# Skip notification if THIS project's VSCode window is focused
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class ForegroundCheck {
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
}
"@
$hwnd = [ForegroundCheck]::GetForegroundWindow()
$sb = New-Object System.Text.StringBuilder 256
[void][ForegroundCheck]::GetWindowText($hwnd, $sb, 256)
$windowTitle = $sb.ToString()
$folder = if ($Cwd) { Split-Path $Cwd -Leaf } else { "" }
if ($folder -and $windowTitle -like "*$folder*Visual Studio Code*") { exit 0 }

Import-Module BurntToast

$text1 = New-BTText -Text $Title
$text2 = New-BTText -Text $Message
$bindingParams = @{ Children = $text1, $text2 }
if ($iconPath -and (Test-Path $iconPath)) {
    $image = New-BTImage -Source $iconPath -AppLogoOverride
    $bindingParams.AppLogoOverride = $image
}
$binding = New-BTBinding @bindingParams
$visual = New-BTVisual -BindingGeneric $binding

$contentParams = @{ Visual = $visual }

# Click opens VSCode to the project folder via protocol handler - no terminal flash
if ($Cwd) {
    $escapedPath = [Uri]::EscapeUriString(($Cwd -replace '\\','/'))
    $uri = "vscode://file/$escapedPath"
    $contentParams.Launch = $uri
    $contentParams.ActivationType = "Protocol"
}

$content = New-BTContent @contentParams
Submit-BTNotification -Content $content

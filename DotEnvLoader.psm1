<#
.SYNOPSIS
    Cross-platform .env file loader for PowerShell.

.DESCRIPTION
    Reads a .env file and loads its key=value pairs into the current process environment.
    Supports comments (# or ;) and ignores blank lines.
    Works on Windows, macOS, and Linux.

.EXAMPLE
    Import-Module ./DotEnvLoader.psm1
    Import-DotEnv -Path "./.env"
#>

function Import-DotEnv {
    [CmdletBinding()]
    param (
        # Path to the .env file
        [Parameter(Mandatory = $false)]
        [string]$Path = ".env",

        # If set, overwrite existing environment variables
        [switch]$Overwrite
    )

    if (-not (Test-Path $Path)) {
        throw "'.env' file not found at path: $Path"
    }

    try {
        Get-Content -Path $Path -Encoding UTF8 | ForEach-Object {
            $line = $_.Trim()

            # Skip empty lines and comments
            if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#") -or $line.StartsWith(";")) {
                return
            }

            # Match KEY=VALUE (allow spaces around '=')
            if ($line -match '^\s*([^=]+?)\s*=\s*(.*)\s*$') {
                $key = $matches[1].Trim()
                $value = $matches[2].Trim()

                # Remove surrounding quotes if present
                if ($value.StartsWith('"') -and $value.EndsWith('"')) {
                    $value = $value.Substring(1, $value.Length - 2)
                }
                elseif ($value.StartsWith("'") -and $value.EndsWith("'")) {
                    $value = $value.Substring(1, $value.Length - 2)
                }

                # Only set if Overwrite is specified or variable doesn't exist
                if ($Overwrite -or -not [System.Environment]::GetEnvironmentVariable($key, "Process")) {
                    [System.Environment]::SetEnvironmentVariable($key, $value, "Process")
                }
            }
        }
    }
    catch {
        throw "Error reading .env file: $_"
    }
}
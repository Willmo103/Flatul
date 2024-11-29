if (-not (Test-Path -Path 'C:\Users\Will\CodeFlattener\counters\RepoScribe-2.2.0_counter.txt')) {
    Set-Content -Path 'C:\Users\Will\CodeFlattener\counters\RepoScribe-2.2.0_counter.txt' -Value "1"
}

# Read the counter value and convert to an integer
$counter = [int](Get-Content -Path 'C:\Users\Will\CodeFlattener\counters\RepoScribe-2.2.0_counter.txt')

# Create a variable to hold the final path
$savePath = 'C:\Users\Will\Desktop\RepoScribe-2.2.0\RepoScribe-2.2.0\.dev\versions\RepoScribe-2.2.0_codebase_v$counter.md'

# Define the command
$command = "'C:\Users\Will\Desktop\RepoScribe-2.2.0\RepoScribe-2.2.0\.dev\CodeFlattener.exe' -i 'C:\Users\Will\Desktop\RepoScribe-2.2.0\RepoScribe-2.2.0' -o '$savePath'"

# Try to run the command
try {
    Invoke-Expression $command
}
catch {
    Write-Error "Failed to run the command: $command"
    exit 1
}

# Try to copy the output to the database folder with the project name
try {
    Copy-Item -Path $savePath -Destination 'C:\Users\Will\.fltn_data\RepoScribe-2.2.0\codebase_v$counter.md' -Force
}
catch {
    Write-Error "Failed to copy the output to the database folder"
}

# Increment the counter
$counter++

# Save the new counter value
Set-Content -Path 'C:\Users\Will\CodeFlattener\counters\RepoScribe-2.2.0_counter.txt' -Value $counter

# Copy the contents of the current version's text file to the clipboard
$version_text = Get-Content -Path $savePath
Set-Clipboard -Value $version_text

# Print that the command was executed successfully
Write-Host "Command executed successfully. The output has been copied to the clipboard."

# Print the output version iteration
Write-Host "Output version: $savePath"

# This process created a log file in the path scanned, we need to add it to the .gitignore file
try {
    $gitignore_path = Join-Path -Path 'C:\Users\Will\Desktop\RepoScribe-2.2.0\RepoScribe-2.2.0' -ChildPath '.gitignore'
    $git_path = Join-Path -Path 'C:\Users\Will\Desktop\RepoScribe-2.2.0\RepoScribe-2.2.0' -ChildPath '.git'
    if (-not (Test-Path -Path $gitignore_path) -and (Test-Path -Path $git_path)) {
        New-Item -Path $gitignore_path -ItemType File -Force
        Add-Content -Path $gitignore_path -Value ".dev`n*RunCodeFlattener*.ps1`n*_codebase_v*.md`nlogs"
    }
    elseif ((Test-Path -Path $gitignore_path) -and (Test-Path -Path $git_path)) {
        Add-Content -Path $gitignore_path -Value ".dev`n*RunCodeFlattener*.ps1`n*_codebase_v*.md`nlogs"
    }
}
catch {
    Write-Error "Failed to update the .gitignore file"
}

# Print that the setup is complete
Write-Host "Operation complete."

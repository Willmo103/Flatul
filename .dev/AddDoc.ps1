# Initialize the counter if it doesn't exist
if (-not (Test-Path -Path "C:\Users\Will\CodeFlattener\counters\RepoScribe-2.2.0_counter.txt")) {
    Set-Content -Path "C:\Users\Will\CodeFlattener\counters\RepoScribe-2.2.0_counter.txt" -Value "1"
}

# Read the counter value and convert to an integer
$counter = [int](Get-Content -Path "C:\Users\Will\CodeFlattener\counters\RepoScribe-2.2.0_counter.txt")

# Get clipboard content
$clipboardContent = Get-Clipboard

if (-not $clipboardContent) {
    Write-Error "Clipboard is empty."
    exit 1
}

# Define save paths
$projectSavePath = "C:\Users\Will\.fltn_data\RepoScribe-2.2.0\clipboard_{0}.md" -f $counter
$aiDocsSavePath = "C:\Users\Will\Desktop\RepoScribe-2.2.0\RepoScribe-2.2.0\.dev\ai_docs\clipboard_{0}.md" -f $counter

# Save clipboard content to project save folder
Set-Content -Path $projectSavePath -Value $clipboardContent

# Save clipboard content to .dev/ai_docs folder
Set-Content -Path $aiDocsSavePath -Value $clipboardContent

# Increment the counter
$counter++

# Save the new counter value
Set-Content -Path "C:\Users\Will\CodeFlattener\counters\RepoScribe-2.2.0_counter.txt" -Value $counter

# Print success messages
Write-Host "Clipboard content saved to project save folder: $projectSavePath"
Write-Host "Clipboard content saved to AI docs folder: $aiDocsSavePath"

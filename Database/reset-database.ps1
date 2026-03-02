# PowerShell script to reset the Orleans database
# This will drop and recreate all Orleans tables

Write-Host "Resetting Orleans PostgreSQL Database..." -ForegroundColor Cyan

# Database connection parameters
$env:PGPASSWORD = "postgres"
$dbHost = "localhost"
$dbPort = "5432"
$dbName = "orleans"
$dbUser = "postgres"

# Check if psql is available
if (!(Get-Command psql -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: psql command not found. Please install PostgreSQL client tools." -ForegroundColor Red
    exit 1
}

Write-Host "Connecting to PostgreSQL at ${dbHost}:${dbPort}..." -ForegroundColor Yellow

# Execute the setup script
$scriptPath = Join-Path $PSScriptRoot "postgres-setup.sql"

if (!(Test-Path $scriptPath)) {
    Write-Host "ERROR: Setup script not found at: $scriptPath" -ForegroundColor Red
    exit 1
}

Write-Host "Executing setup script..." -ForegroundColor Yellow

try {
    psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -f $scriptPath
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nDatabase reset completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "`nDatabase reset failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "`nError executing script: $_" -ForegroundColor Red
    exit 1
}

# Verify tables were created
Write-Host "`nVerifying tables..." -ForegroundColor Yellow
psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -c "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name LIKE 'Orleans%' ORDER BY table_name;"

Write-Host "`nDone!" -ForegroundColor Green


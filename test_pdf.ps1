# Test PDF generation endpoint
Write-Host "Testing PDF generation endpoint..."

# Wait for backend to be ready
Write-Host "Waiting for backend to start..."
Start-Sleep -Seconds 8

# Test PDF endpoint
Write-Host "Calling http://localhost:5000/api/sales/24/pdf..."
try {
    $uri = "http://localhost:5000/api/sales/24/pdf"
    $response = Invoke-WebRequest -Uri $uri -Method Get -ErrorVariable webError
    Write-Host "✅ Success! Status Code: $($response.StatusCode)"
    Write-Host "PDF Size: $($response.Content.Length) bytes"
} catch {
    Write-Host "❌ Error calling PDF endpoint"
    Write-Host "Error Message: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
        Write-Host "Status Description: $($_.Exception.Response.StatusDescription)"
    }
    Write-Host "Full Error: $_"
}

Write-Host ""
Write-Host "Test completed."

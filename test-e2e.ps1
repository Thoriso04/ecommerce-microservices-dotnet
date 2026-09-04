# ==============================================================================
# End-to-End Microservices Test Script (PowerShell)
# ==============================================================================

$GatewayUrl = "http://localhost:5000"
$DirectUserUrl = "http://localhost:5001"
$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Starting End-to-End Microservices Verification   " -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# ------------------------------------------------------------------------------
# 0. Health & Route Warmup Check
# ------------------------------------------------------------------------------
Write-Host "`n[0/5] Checking Gateway Routing Health..." -ForegroundColor Yellow

$maxRetries = 5
$retryCount = 0
$gatewayReady = $false

while (-not $gatewayReady -and $retryCount -lt $maxRetries) {
    try {
        $testPayload = @{
            username = "healthcheck_user"
            email    = "healthcheck@test.com"
            password = "Passw0rd123!"
        } | ConvertTo-Json

        # Issue dummy call to test gateway resolution
        $null = Invoke-RestMethod -Uri "$GatewayUrl/api/users/register" -Method Post -ContentType "application/json" -Body $testPayload
        $gatewayReady = $true
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 400 -or $statusCode -eq 409) {
            # User might already exist or validation failed, but Gateway ROUTED it successfully!
            $gatewayReady = $true
        } else {
            $retryCount++
            Write-Host "  Gateway routing warm-up attempt $retryCount/$maxRetries failed (Status: $statusCode). Waiting 2s..." -ForegroundColor DarkYellow
            Start-Sleep -Seconds 2
        }
    }
}

if (-not $gatewayReady) {
    Write-Host "  [WARNING] Gateway still returning error via Consul. Check health checks in Consul at http://localhost:8500" -ForegroundColor Red
} else {
    Write-Host "  [OK] Gateway successfully routing to user-service!" -ForegroundColor Green
}

# Generate unique run ID to avoid duplicate email/username DB constraints
$timestamp = Get-Date -Format "HHmmss"
$testUser = "user_$timestamp"
$testEmail = "user_$timestamp@test.com"
$testPass = "Passw0rd123!"

# ------------------------------------------------------------------------------
# 1. User Registration
# ------------------------------------------------------------------------------
Write-Host "`n[1/5] Registering User ($testUser)..." -ForegroundColor Yellow

$registerBody = @{
    username = $testUser
    email    = $testEmail
    password = $testPass
} | ConvertTo-Json

try {
    $registerRes = Invoke-RestMethod -Uri "$GatewayUrl/api/users/register" -Method Post -ContentType "application/json" -Body $registerBody
    Write-Host "  [SUCCESS] User Registered with ID: $($registerRes.id)" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] User registration failed: $_" -ForegroundColor Red
    exit 1
}

# ------------------------------------------------------------------------------
# 2. User Login
# ------------------------------------------------------------------------------
Write-Host "`n[2/5] Logging In User..." -ForegroundColor Yellow

$loginBody = @{
    username = $testUser
    password = $testPass
} | ConvertTo-Json

try {
    $loginRes = Invoke-RestMethod -Uri "$GatewayUrl/api/users/login" -Method Post -ContentType "application/json" -Body $loginBody
    $token = $loginRes.token
    Write-Host "  [SUCCESS] JWT Token obtained successfully." -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Login failed: $_" -ForegroundColor Red
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
}

# ------------------------------------------------------------------------------
# 3. Product Catalog Operations
# ------------------------------------------------------------------------------
Write-Host "`n[3/5] Testing Product Service..." -ForegroundColor Yellow

# Create Product
$productBody = @{
    name        = "Wireless Gaming Mouse $timestamp"
    description = "High precision optical gaming mouse"
    price       = 59.99
    stock       = 100
} | ConvertTo-Json

try {
    $productRes = Invoke-RestMethod -Uri "$GatewayUrl/api/products" -Method Post -ContentType "application/json" -Body $productBody -Headers $headers
    $productId = $productRes.id
    Write-Host "  [SUCCESS] Product created with ID: $productId" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Product creation failed: $_" -ForegroundColor Red
    exit 1
}

# Fetch Products
try {
    $products = Invoke-RestMethod -Uri "$GatewayUrl/api/products" -Method Get -Headers $headers
    Write-Host "  [SUCCESS] Retreived $($products.Count) products from catalog." -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Fetching products failed: $_" -ForegroundColor Red
    exit 1
}

# ------------------------------------------------------------------------------
# 4. Order Creation
# ------------------------------------------------------------------------------
Write-Host "`n[4/5] Creating Order..." -ForegroundColor Yellow

$orderBody = @{
    items = @(
        @{
            productId = $productId
            quantity  = 2
            unitPrice = 59.99
        }
    )
} | ConvertTo-Json -Depth 3

try {
    $orderRes = Invoke-RestMethod -Uri "$GatewayUrl/api/orders" -Method Post -ContentType "application/json" -Body $orderBody -Headers $headers
    $orderId = $orderRes.id
    Write-Host "  [SUCCESS] Order created with ID: $orderId" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Order creation failed: $_" -ForegroundColor Red
    exit 1
}

# ------------------------------------------------------------------------------
# 5. Fetch User Orders
# ------------------------------------------------------------------------------
Write-Host "`n[5/5] Fetching User Orders..." -ForegroundColor Yellow

try {
    $orders = Invoke-RestMethod -Uri "$GatewayUrl/api/orders" -Method Get -Headers $headers
    Write-Host "  [SUCCESS] Successfully retrieved $($orders.Count) orders." -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Fetching user orders failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`n==================================================" -ForegroundColor Cyan
Write-Host "  ALL END-TO-END TESTS PASSED SUCCESSFULLY!       " -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Cyan
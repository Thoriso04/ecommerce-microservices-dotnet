#!/bin/bash
set -e
BASE="http://localhost:5000"

echo -e "\n=== 1. Registering user 'alice' ==="
curl -s -X POST "$BASE/api/users/register" \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","email":"alice@test.com","password":"Passw0rd!"}' || true

echo -e "\n=== 2. Logging in ==="
LOGIN_RESPONSE=$(curl -s -X POST "$BASE/api/users/login" \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","password":"Passw0rd!"}')
TOKEN=$(echo "$LOGIN_RESPONSE" | grep -o '"token":"[^"]*' | cut -d'"' -f4)
echo "Token: ${TOKEN:0:30}..."

echo -e "\n=== 3. Listing products ==="
curl -s "$BASE/api/products" | head -c 500
echo ""

PRODUCT_ID="11111111-1111-1111-1111-111111111111"

echo -e "\n=== 4. Creating order ==="
curl -s -X POST "$BASE/api/orders" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"productId\":\"$PRODUCT_ID\",\"quantity\":2}"
echo ""

echo -e "\n=== 5. Confirming stock decremented ==="
curl -s "$BASE/api/products/$PRODUCT_ID"
echo ""

echo -e "\n=== 6. Health checks ==="
for port in 5001 5002 5003; do
  echo "Port $port:"
  curl -s "http://localhost:$port/health"
  echo ""
done

echo -e "\n=== 7. Circuit breaker demo: stopping product-service ==="
docker compose stop product-service
sleep 3
echo "Attempting order while Product Service is down..."
curl -s -X POST "$BASE/api/orders" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"productId\":\"$PRODUCT_ID\",\"quantity\":1}" || true
echo ""
echo "Check Seq (http://localhost:8081) or: docker compose logs order-service --tail 50"

echo -e "\n=== 8. Restarting product-service ==="
docker compose start product-service
sleep 5

echo -e "\n=== 9. Confirming recovery ==="
curl -s -X POST "$BASE/api/orders" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"productId\":\"$PRODUCT_ID\",\"quantity\":1}"
echo ""

echo -e "\n=== ALL TESTS COMPLETE ==="
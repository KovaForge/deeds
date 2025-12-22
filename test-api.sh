#!/bin/bash

API_BASE="${API_BASE:-https://calm-coast-030581510.1.azurestaticapps.net/api}"

echo "=== Good Deeds API Test Script ==="
echo "API Base: $API_BASE"
echo

# Function to create a parent
create_parent() {
    local email="$1"
    echo "Creating parent with email: $email"
    
    response=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/parents" \
        -H "Content-Type: application/json" \
        -d "{\"email\":\"$email\"}")
    
    http_code=$(echo "$response" | tail -n1)
    body=$(echo "$response" | head -n-1)
    
    echo "HTTP Status: $http_code"
    echo "Response: $body"
    
    if [ "$http_code" != "201" ] && [ "$http_code" != "200" ]; then
        echo "ERROR: Failed to create parent (HTTP $http_code)"
        return 1
    fi
    
    parent_id=$(echo "$body" | jq -r '.id' 2>/dev/null || echo "")
    
    if [ -z "$parent_id" ] || [ "$parent_id" = "null" ]; then
        echo "ERROR: Failed to parse parent ID from response"
        return 1
    fi
    
    echo "✓ Parent created with ID: $parent_id"
    echo
    echo "$parent_id"
}

# Function to find a parent by email
find_parent() {
    local email="$1"
    echo "Finding parent with email: $email"
    
    response=$(curl -s -w "\n%{http_code}" -X GET "$API_BASE/parents?email=$(printf %s "$email" | jq -sRr @uri)")
    
    http_code=$(echo "$response" | tail -n1)
    body=$(echo "$response" | head -n-1)
    
    echo "HTTP Status: $http_code"
    echo "Response: $body"
    
    if [ "$http_code" = "404" ]; then
        echo "Parent not found, creating new parent..."
        create_parent "$email"
    elif [ "$http_code" != "200" ]; then
        echo "ERROR: API returned HTTP $http_code"
        return 1
    else
        parent_id=$(echo "$body" | jq -r '.id' 2>/dev/null || echo "")
        if [ -z "$parent_id" ] || [ "$parent_id" = "null" ]; then
            echo "Parent not found, creating new parent..."
            create_parent "$email"
        else
            echo "✓ Found parent with ID: $parent_id"
            echo
            echo "$parent_id"
        fi
    fi
}

# Function to create a child
create_child() {
    local parent_id="$1"
    local name="$2"
    local dollar_per_point="${3:-1.0}"
    
    echo "Creating child '$name' for parent $parent_id"
    
    response=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/children" \
        -H "Content-Type: application/json" \
        -H "x-parent-id: $parent_id" \
        -d "{\"parentId\":\"$parent_id\",\"name\":\"$name\",\"dollarPerPoint\":$dollar_per_point}")
    
    http_code=$(echo "$response" | tail -n1)
    body=$(echo "$response" | head -n-1)
    
    echo "HTTP Status: $http_code"
    echo "Response: $body"
    
    if [ "$http_code" != "201" ]; then
        echo "ERROR: Failed to create child (HTTP $http_code)"
        exit 1
    fi
    
    child_id=$(echo "$body" | jq -r '.id')
    echo "✓ Child created with ID: $child_id"
    echo
}

# Function to list children
list_children() {
    local parent_id="$1"
    
    echo "Listing children for parent $parent_id"
    
    response=$(curl -s -X GET "$API_BASE/parents/$parent_id/children" \
        -H "x-parent-id: $parent_id")
    
    echo "Response: $response"
    count=$(echo "$response" | jq 'length')
    echo "✓ Found $count children"
    echo
}

# Main test flow
main() {
    local email="${1:-test@example.com}"
    local child_name="${2:-Khloe}"
    
    echo "=== Test: Find or Create Parent ==="
    parent_id=$(find_parent "$email")
    
    echo "=== Test: Create Child ==="
    create_child "$parent_id" "$child_name" 1.0
    
    echo "=== Test: List Children ==="
    list_children "$parent_id"
    
    echo "=== All tests passed! ==="
    echo "Parent ID: $parent_id"
}

# Run main with arguments
main "$@"

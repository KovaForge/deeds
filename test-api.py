#!/usr/bin/env python3
"""
Good Deeds API Test Script - Programmatic interface for API testing
"""

import requests
import json
import sys

class GoodDeedsApiClient:
    def __init__(self, base_url="https://calm-coast-030581510.1.azurestaticapps.net/api"):
        self.base_url = base_url.rstrip('/')
        self.session = requests.Session()
    
    def create_parent(self, email: str):
        """Create a new parent"""
        print(f"Creating parent with email: {email}")
        response = self.session.post(
            f"{self.base_url}/parents",
            json={"email": email},
            headers={"Content-Type": "application/json"}
        )
        
        if response.status_code not in [200, 201]:
            print(f"✗ Failed (HTTP {response.status_code}): {response.text}")
            return None
        
        data = response.json()
        # API returns PascalCase keys
        parent_id = data.get('Id') or data.get('id')
        print(f"✓ Parent created with ID: {parent_id}")
        return data
    
    def find_parent(self, email: str):
        """Find an existing parent by email"""
        print(f"Finding parent with email: {email}")
        response = self.session.get(
            f"{self.base_url}/parents",
            params={"email": email}
        )
        
        if response.status_code == 404:
            print(f"Parent not found")
            return None
        
        if response.status_code != 200:
            print(f"✗ Failed (HTTP {response.status_code}): {response.text}")
            return None
        
        data = response.json()
        # API returns PascalCase keys
        parent_id = data.get('Id') or data.get('id')
        print(f"✓ Found parent with ID: {parent_id}")
        return data
    
    def get_or_create_parent(self, email: str):
        """Get existing parent or create if doesn't exist"""
        parent = self.find_parent(email)
        if parent is None:
            parent = self.create_parent(email)
        return parent
    
    def create_child(self, parent_id: str, name: str, dollar_per_point: float = 1.0):
        """Create a new child"""
        print(f"Creating child '{name}' for parent {parent_id} (${dollar_per_point}/point)")
        response = self.session.post(
            f"{self.base_url}/children",
            json={
                "parentId": parent_id,
                "name": name,
                "dollarPerPoint": dollar_per_point
            },
            headers={
                "Content-Type": "application/json",
                "x-parent-id": parent_id
            }
        )
        
        if response.status_code not in [200, 201]:
            print(f"✗ Failed (HTTP {response.status_code}): {response.text}")
            return None
        
        data = response.json()
        # API returns PascalCase keys
        child_id = data.get('Id') or data.get('id')
        print(f"✓ Child created with ID: {child_id}")
        return data
    
    def list_children(self, parent_id: str):
        """List all children for a parent"""
        print(f"Listing children for parent {parent_id}")
        response = self.session.get(
            f"{self.base_url}/parents/{parent_id}/children",
            headers={"x-parent-id": parent_id}
        )
        
        if response.status_code != 200:
            print(f"✗ Failed (HTTP {response.status_code}): {response.text}")
            return []
        
        data = response.json()
        # API returns PascalCase keys
        for child in data:
            child_id = child.get('Id') or child.get('id')
            name = child.get('Name') or child.get('name')
            rate = child.get('DollarPerPoint') or child.get('dollarPerPoint')
            print(f"  - {name} (ID: {child_id}, Rate: ${rate}/point)")
        return data
    
    def test_full_flow(self, parent_email: str = "test@example.com", child_name: str = "Khloe"):
        """Test the full flow: create/find parent and create child"""
        print("\n=== Good Deeds API Test ===\n")
        
        # Get or create parent
        parent = self.get_or_create_parent(parent_email)
        if not parent:
            print("✗ Failed to get or create parent")
            return False
        
        parent_id = parent['id'] if 'id' in parent else parent.get('Id')
        print()
        
        # Create child
        child = self.create_child(parent_id, child_name)
        if not child:
            print("✗ Failed to create child")
            return False
        
        print()
        
        # List children
        children = self.list_children(parent_id)
        print()
        
        print("✓ All tests passed!")
        return True

def main():
    if __name__ != "__main__":
        return
    
    # Get email and child name from command line or use defaults
    email = sys.argv[1] if len(sys.argv) > 1 else "test@example.com"
    child_name = sys.argv[2] if len(sys.argv) > 2 else "Khloe"
    
    client = GoodDeedsApiClient()
    success = client.test_full_flow(email, child_name)
    sys.exit(0 if success else 1)

if __name__ == "__main__":
    main()

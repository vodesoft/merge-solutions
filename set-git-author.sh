#!/bin/bash

# Script to set Git author name and email for the repository and all submodules
# Prompts for values with current effective values as defaults
# Writes to local repository config (.git/config)

# Find the actual git repository root (where .git is located)
REPO_ROOT=$(git rev-parse --show-toplevel 2>/dev/null)
if [ -z "$REPO_ROOT" ]; then
    echo "Error: Not inside a git repository."
    exit 1
fi

cd "$REPO_ROOT" || exit 1

echo "=== Set Git Author Information ==="
echo "Repository: $REPO_ROOT"
echo ""

# Get current effective name and email (local with global fallback) for display
current_name=$(git config user.name 2>/dev/null)
current_email=$(git config user.email 2>/dev/null)

# Prompt for name
echo -n "Enter author name [${current_name:-<empty>}]: "
read -r input_name
new_name="${input_name:-$current_name}"

# Prompt for email
echo -n "Enter author email [${current_email:-<empty>}]: "
read -r input_email
new_email="${input_email:-$current_email}"

# Validate email format (basic check)
if [[ -n "$new_email" && ! "$new_email" =~ ^[^@]+@[^@]+\.[^@]+$ ]]; then
    echo "Warning: Email format may be invalid: $new_email"
    echo -n "Continue anyway? (y/N): "
    read -r confirm
    if [[ ! "$confirm" =~ ^[Yy] ]]; then
        echo "Cancelled."
        exit 1
    fi
fi

# Set for main repo (local config only)
git config --local user.name "$new_name"
git config --local user.email "$new_email"
echo "Set main repo: $new_name <$new_email>"

# Set for all submodules
if [ -f ".gitmodules" ]; then
    echo ""
    echo "Updating submodules..."
    
    # Get list of submodules
    submodules=$(git config --file .gitmodules --get-regexp 'path' | awk '{print $2}')
    
    if [ -z "$submodules" ]; then
        echo "No submodules found."
    else
        for module_path in $submodules; do
            # Get submodule name from path
            module_name=$(git config --file .gitmodules --get "submodule.$module_path.name")
            
            # Set config in submodule local config
            if [ -d "$module_path" ]; then
                git -C "$module_path" config --local user.name "$new_name"
                git -C "$module_path" config --local user.email "$new_email"
                echo "Set submodule '$module_name' ($module_path): $new_name <$new_email>"
            else
                echo "Warning: Submodule directory not found: $module_path"
            fi
        done
    fi
else
    echo ""
    echo "No .gitmodules file found. No submodules to update."
fi

echo ""
echo "=== Done ==="
echo "Current configuration:"
echo "  Name:  $(git config --local user.name 2>/dev/null || git config user.name)"
echo "  Email: $(git config --local user.email 2>/dev/null || git config user.email)"

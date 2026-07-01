#!/bin/bash
# Run this script once to create all project labels on GitHub.
# Requires GitHub CLI (gh) to be installed and authenticated.
# Usage: bash .github/create-labels.sh <owner/repo>

REPO=${1:?"Usage: $0 <owner/repo>"}

# List of default GitHub labels to remove
DEFAULT_LABELS=(
  "bug"
  "documentation"
  "duplicate"
  "enhancement"
  "good first issue"
  "help wanted"
  "invalid"
  "question"
  "wontfix"
)

echo "Removing default GitHub labels from $REPO..."

for label in "${DEFAULT_LABELS[@]}"; do
  gh label delete "$label" --repo "$REPO" --yes 2>/dev/null
done

echo "Creating new labels for $REPO..."

# Type
gh label create "bug" --repo "$REPO" --color "d73a4a" --description "Bug or unexpected behavior" --force
gh label create "feature" --repo "$REPO" --color "a2eeef" --description "New feature or improvement" --force
gh label create "task" --repo "$REPO" --color "e4e669" --description "Internal development task" --force

# Priority
gh label create "priority: high" --repo "$REPO" --color "b60205" --description "Needs immediate attention" --force
gh label create "priority: medium" --repo "$REPO" --color "fbca04" --description "Normal priority" --force
gh label create "priority: low" --repo "$REPO" --color "0075ca" --description "Can be deferred" --force

# Status
gh label create "blocked" --repo "$REPO" --color "ee0701" --description "Blocked by another issue" --force
gh label create "needs discussion" --repo "$REPO" --color "cc317c" --description "Requires a decision first" --force

# Area
gh label create "area: core" --repo "$REPO" --color "7f2a94" --description "Related to core functionality" --force
gh label create "area: cli" --repo "$REPO" --color "6e6e6e" --description "Related to command-line interface" --force
gh label create "area: avalonia" --repo "$REPO" --color "1ca8dd" --description "Related to Avalonia UI framework" --force

# Dependencies
gh label create "dependencies" --repo "$REPO" --color "0366d6" --description "Pull requests that update a dependency file" --force

echo "Done. All labels created for $REPO."
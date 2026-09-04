#!/usr/bin/env bash
# macOS / Linux launcher for the Ferris Wheel mock API.
set -euo pipefail
cd "$(dirname "$0")"

export PATH="/opt/homebrew/bin:/usr/local/bin:/opt/local/bin:$PATH"

if ! command -v node >/dev/null 2>&1; then
  echo "Node.js not found. Install from https://nodejs.org then retry."
  exit 1
fi

if [ ! -d node_modules/express ]; then
  echo "Installing npm dependencies..."
  npm install
fi

echo
echo "Starting Ferris Wheel Dev API Server..."
echo "Port  : 5002"
echo "API   : http://127.0.0.1:5002/api/v1"
echo "Test  : http://127.0.0.1:5002/index.html"
echo
echo "Press Ctrl+C to stop."
echo

exec npm start

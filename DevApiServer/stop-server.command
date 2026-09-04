#!/bin/bash
# Double-click this on macOS (same role as stop-server.bat on Windows).
cd "$(dirname "$0")"
chmod +x "./stop-server.sh" 2>/dev/null || true
./stop-server.sh
echo
read -r -p "Press Enter to close..."

#!/bin/bash
# Double-click this on macOS (same role as start-server.bat on Windows).
cd "$(dirname "$0")"
chmod +x "./start-server.sh" "./stop-server.sh" 2>/dev/null || true
./start-server.sh
echo
read -r -p "Press Enter to close..."

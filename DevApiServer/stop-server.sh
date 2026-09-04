#!/usr/bin/env bash
# macOS / Linux: stop whatever is listening on the mock API port.
set -u
PORT=5002

PIDS=$(lsof -nP -iTCP:${PORT} -sTCP:LISTEN -t 2>/dev/null || true)
if [ -z "${PIDS}" ]; then
  echo "No Ferris Wheel Dev API Server listening on port ${PORT}."
  exit 0
fi

echo "Stopping Ferris Wheel Dev API Server (port ${PORT}): ${PIDS}"
kill ${PIDS}
echo "Done."

#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_DIR="$ROOT_DIR/api"
APP_DIR="$ROOT_DIR/app"

if [[ -z "${DB:-}" ]]; then
  echo "Warning: DB environment variable is not set. The Functions API will exit immediately without a Postgres connection string." >&2
fi

if ! command -v func >/dev/null 2>&1; then
  echo "Azure Functions Core Tools ('func') is required to run the API locally." >&2
  exit 1
fi

FUNC_PID=""
cleanup() {
  if [[ -n "$FUNC_PID" ]] && kill -0 "$FUNC_PID" 2>/dev/null; then
    kill "$FUNC_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

( cd "$API_DIR" && func start ) &
FUNC_PID=$!

cd "$APP_DIR"
dotnet watch run

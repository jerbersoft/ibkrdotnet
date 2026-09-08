#!/usr/bin/env bash
#
# Downloads the Interactive Brokers Web API reference documentation as Markdown into
# artifacts/spec/ (gitignored). IBKR serves a clean Markdown rendering of every docs page by
# appending `.md` to its URL, and publishes a machine-readable index at /docs/llms.txt.
#
# We deliberately do not vendor these files into the repository: they are IBKR's documentation,
# not ours. This script makes them reproducible instead.
#
# Usage:  ./tools/fetch-spec.sh [output-dir]
#
set -euo pipefail

OUT_DIR="${1:-artifacts/spec}"
INDEX_URL="https://www.interactivebrokers.com/docs/web-api/llms.txt"
UA="ibkrdotnet-spec-fetch/1.0"

mkdir -p "$OUT_DIR"
echo "Fetching index: $INDEX_URL"
curl -sfL -A "$UA" "$INDEX_URL" -o "$OUT_DIR/llms.txt"

# Pull every Trading reference page, the authentication guides, and the top-level Web API docs.
grep -oE 'https://ibkrcampus\.com/docs/web-api/[^ )]*\.md' "$OUT_DIR/llms.txt" \
  | grep -E 'api-reference/trading/|api-reference/authentication/|/authentication/|/api/web-api/|/introduction\.md|/getting-started\.md' \
  | sort -u > "$OUT_DIR/.urls"

count=$(wc -l < "$OUT_DIR/.urls" | tr -d ' ')
echo "Downloading $count pages into $OUT_DIR ..."

while read -r url; do
    rel="${url#https://ibkrcampus.com/docs/web-api/}"
    dest="$OUT_DIR/$rel"
    mkdir -p "$(dirname "$dest")"
    curl -sfL -A "$UA" "$url" -o "$dest" || echo "  FAILED: $url" >&2
done < "$OUT_DIR/.urls"

rm -f "$OUT_DIR/.urls"
echo "Done. $(find "$OUT_DIR" -name '*.md' | wc -l | tr -d ' ') Markdown files in $OUT_DIR"

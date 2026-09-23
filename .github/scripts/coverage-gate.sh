#!/usr/bin/env bash
# Fails the build when overall line coverage (Coverlet's cobertura report) is below $1 percent.
set -euo pipefail
min="$1"
report=$(find coverage -name coverage.cobertura.xml | head -1)
rate=$(grep -o '<coverage line-rate="[0-9.]*"' "$report" | cut -d'"' -f2)
pct=$(awk -v r="$rate" 'BEGIN { printf "%.1f", r * 100 }')
echo "Line coverage: ${pct}% (minimum ${min}%)"
awk -v p="$pct" -v m="$min" 'BEGIN { exit !(p >= m) }' || { echo "::error::Line coverage ${pct}% is below ${min}%"; exit 1; }

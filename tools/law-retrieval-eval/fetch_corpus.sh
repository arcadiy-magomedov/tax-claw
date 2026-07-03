#!/usr/bin/env bash
# Reproducibly pull the §586/1992 (Income Tax Act) 2027 edition text fragments from the official
# e-Sbírka open-data SPARQL endpoint (keyless, public-domain, updated daily). Output: corpus586_2027.json
set -euo pipefail
cd "$(dirname "$0")"

read -r -d '' QUERY <<'SPARQL' || true
PREFIX p: <https://slovník.gov.cz/datový/sbírka/pojem/>
SELECT ?cit ?text WHERE {
  <https://opendata.eselpoint.gov.cz/esel-esb/eli/cz/sb/1992/586/2027-01-01> p:má-fragment-znění ?f .
  ?f p:citace-označení-fragmentu-znění-právního-aktu ?cit .
  ?f p:obsahuje-fragment ?tf .
  ?tf p:text-fragmentu ?text .
}
SPARQL

curl -sS -m 180 "https://opendata.eselpoint.gov.cz/sparql" \
  --data-urlencode "query=${QUERY}" \
  -H "Accept: application/sparql-results+json" \
  -o corpus586_2027.json

echo "Wrote corpus586_2027.json ($(du -h corpus586_2027.json | cut -f1))"

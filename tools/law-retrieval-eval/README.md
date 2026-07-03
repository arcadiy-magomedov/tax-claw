# Law retrieval eval (§586/1992)

Measures whether **keyword search is enough** to retrieve the correct section (§) of the Czech
Income Tax Act, before committing to a retrieval architecture for tax-claw's law grounding.

## Data
- `fetch_corpus.sh` pulls the **2027 edition** of act 586/1992 from the official **e-Sbírka**
  open-data SPARQL endpoint (`opendata.eselpoint.gov.cz`, keyless, public-domain, daily-updated).
- Output `corpus586_2027.json` → 2920 text fragments → aggregated to **199 § documents**.
- The source is natively structured (`část → § → odstavec → písmeno → bod`) with official citation
  labels, so **no text parsing/regex is needed** — ingestion is a structured import.

## Method
- SQLite **FTS5**, `unicode61 remove_diacritics 2` (Czech-aware, folds diacritics), bm25 ranking.
- One document per §. Target metric: is the correct § in top-k? → Recall@{1,3,5}, MRR.
- Two strategies over the same gold set (`gold.json`, 22 queries):
  - **A. raw query (EN)** — natural user/agent phrasing.
  - **B. + LLM Czech expansion** — query translated/expanded to Czech legal terms
    (authored as an LLM expander would; a runtime expander would replicate this).

Run: `python3 eval.py` (offline; regenerate data with `./fetch_corpus.sh`).

## Results (2027 edition, 22 queries)

| strategy                   | R@1 | R@3 | R@5 | MRR |
|----------------------------|-----|-----|-----|-----|
| A. raw query (EN)          | 0.00| 0.00| 0.00| 0.00|
| B. + LLM Czech expansion   | 0.50| 0.77| 0.77| 0.61|

## Conclusions
1. **Querying in Czech legal vocabulary is mandatory.** Raw EN keyword = 0 recall (no shared
   tokens). The agent MUST translate/expand queries to Czech before any lexical search.
2. **Keyword + expansion is strong for *discovery* queries** (RSU, dividends, sale of securities,
   time-test exemption, foreign-tax-credit, deductions → mostly rank 1–3) but **insufficient alone
   overall** (0.77 R@5).
3. **Misses are "definitional/structural" §s with high-frequency vocabulary** — tax rate (§16),
   taxpayer/residence (§2), tax base (§5), taxpayer credit (§35ba), return obligation (§38g).
   Common words ("daně", "poplatník", "základ daně") flood bm25 and bury the defining §.
4. **These misses are exactly the cases addressed-lookup covers**: the agent knows §16/§2/§5/§38g
   from the form/instructions structure and would `lookup_law(§)`, not search.

## Recommendation
- **v1 = addressed `lookup_law(§)` (primary) + FTS5 keyword with mandatory Czech query-expansion
  (discovery).** Covers discovery well; definitional gaps are handled by lookup.
- **Defer vector**, keep the seam. If discovery on definitional queries must harden, the next
  experiment is a *targeted* vector test on the miss set (needs an embedding provider; note the
  default Copilot provider likely has none).

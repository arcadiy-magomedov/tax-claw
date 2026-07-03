#!/usr/bin/env python3
"""Retrieval eval for the §586/1992 law corpus.

Compares keyword search (SQLite FTS5, Czech-aware unicode61 + diacritic folding) on:
  A. raw natural-language query (as a user/agent might type it), vs
  B. an LLM-authored Czech legal-term expansion of the same query.

Documents = one per § (aggregating the section's text fragments). Metric target: is the
correct § retrieved? Reports Recall@{1,3,5} and MRR. No network; reads corpus586_2027.json.
"""
import json, re, sqlite3, sys
from collections import OrderedDict
from pathlib import Path

HERE = Path(__file__).parent
K_VALUES = (1, 3, 5)


def load_docs():
    """Aggregate fragment texts into one document per top-level section (§ N)."""
    rows = json.loads((HERE / "corpus586_2027.json").read_text())["results"]["bindings"]
    docs = OrderedDict()
    for r in rows:
        m = re.match(r"§\s*(\d+[a-z]*)", r["cit"]["value"].strip())
        if not m:
            continue  # skip structural headers ("Část 1", etc.)
        section = f"§ {m.group(1)}"
        text = re.sub(r"<[^>]+>", " ", r["text"]["value"])
        docs.setdefault(section, []).append(text)
    return OrderedDict((s, re.sub(r"\s+", " ", " ".join(t)).strip()) for s, t in docs.items())


def build_index(docs):
    db = sqlite3.connect(":memory:")
    db.execute("CREATE VIRTUAL TABLE d USING fts5(section UNINDEXED, body, "
               "tokenize='unicode61 remove_diacritics 2')")
    db.executemany("INSERT INTO d(section, body) VALUES (?,?)", docs.items())
    return db


def to_match(query):
    toks = [t for t in re.findall(r"\w+", query, re.UNICODE) if len(t) >= 2]
    return " OR ".join('"%s"' % t for t in toks) if toks else None


def search(db, query, k=max(K_VALUES)):
    match = to_match(query)
    if not match:
        return []
    cur = db.execute(
        "SELECT section FROM d WHERE d MATCH ? ORDER BY bm25(d) LIMIT ?", (match, k))
    return [row[0] for row in cur.fetchall()]


def score(queries, db, field):
    recall = {k: 0 for k in K_VALUES}
    rr_sum = 0.0
    per_query = []
    for q in queries:
        hits = search(db, q[field])
        gold = set(q["gold"])
        first = next((i + 1 for i, s in enumerate(hits) if s in gold), None)
        for k in K_VALUES:
            if any(s in gold for s in hits[:k]):
                recall[k] += 1
        rr_sum += (1.0 / first) if first else 0.0
        per_query.append((q["id"], first, hits[:3]))
    n = len(queries)
    return {"recall": {k: recall[k] / n for k in K_VALUES}, "mrr": rr_sum / n, "detail": per_query}


def main():
    docs = load_docs()
    gold_data = json.loads((HERE / "gold.json").read_text())
    queries = gold_data["queries"]

    # Validate gold labels against the corpus.
    missing = sorted({s for q in queries for s in q["gold"] if s not in docs})
    if missing:
        print(f"⚠️  gold sections not found in corpus (fix gold.json): {missing}\n")

    db = build_index(docs)
    print(f"Corpus: {len(docs)} § documents | {len(queries)} gold queries\n")

    results = {}
    for label, field in (("A. raw query (EN)", "query"), ("B. + LLM Czech expansion", "query_cz")):
        results[label] = score(queries, db, field)

    print(f"{'strategy':<28} {'R@1':>6} {'R@3':>6} {'R@5':>6} {'MRR':>6}")
    print("-" * 56)
    for label, r in results.items():
        print(f"{label:<28} {r['recall'][1]:>6.2f} {r['recall'][3]:>6.2f} "
              f"{r['recall'][5]:>6.2f} {r['mrr']:>6.2f}")

    print("\nPer-query first-hit rank (— = miss in top 5):")
    print(f"{'query id':<22} {'raw':>4} {'+cz':>4}")
    a = {i: (rk, top) for i, rk, top in results['A. raw query (EN)']['detail']}
    b = {i: (rk, top) for i, rk, top in results['B. + LLM Czech expansion']['detail']}
    for q in queries:
        ra = a[q["id"]][0] or "—"
        rb = b[q["id"]][0] or "—"
        print(f"{q['id']:<22} {str(ra):>4} {str(rb):>4}")


if __name__ == "__main__":
    main()

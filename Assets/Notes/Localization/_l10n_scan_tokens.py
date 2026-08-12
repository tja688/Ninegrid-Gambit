# -*- coding: utf-8 -*-
"""统计 live 中文源里的 [[词条]] / {令牌} / [code] 全集"""
import json, io, re, os, collections

ROOT = r"C:\Users\jinji\Documents\GitHub\Ninegrid Gambit"
SRC = os.path.join(ROOT, "Assets", "Notes", "Localization", "_l10n_zh_extract.json")

with io.open(SRC, "r", encoding="utf-8") as f:
    data = json.load(f)

rx_term = re.compile(r"\[\[([^\]]+)\]\]")
rx_token = re.compile(r"\{([^{}]+)\}")
rx_code = re.compile(r"\[([^\[\]]+)\]")

terms = collections.Counter()
codes = collections.Counter()
token_count = 0
for cid, e in data["entries"].items():
    for field in ("displayName", "description", "faceIntro"):
        t = e.get(field)
        if not t:
            continue
        for m in rx_term.findall(t):
            terms[m.strip()] += 1
        stripped = rx_term.sub("", t)
        token_count += len(rx_token.findall(stripped))
        for m in rx_code.findall(stripped):
            if "{" not in m:
                codes[m.strip()] += 1

print("== [[terms]] (%d distinct) ==" % len(terms))
for k, v in sorted(terms.items(), key=lambda kv: -kv[1]):
    print("%3d  %s" % (v, k))
print("\n== [codes] (%d distinct) ==" % len(codes))
for k, v in sorted(codes.items(), key=lambda kv: -kv[1]):
    print("%3d  %s" % (v, k))
print("\ntotal {tokens}:", token_count)

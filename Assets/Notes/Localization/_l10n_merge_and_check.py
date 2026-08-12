# -*- coding: utf-8 -*-
"""合并分批稿 -> cards.json，并跑 5 项自检 + 附加一致性检查"""
import json, io, os, re, sys

ROOT = r"C:\Users\jinji\Documents\GitHub\Ninegrid Gambit"
NOTES = os.path.join(ROOT, "Assets", "Notes", "Localization")
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Localization", "en")
CARDS_OUT = os.path.join(OUT_DIR, "cards.json")
GLOSSARY = os.path.join(OUT_DIR, "glossary.json")
EXTRACT = os.path.join(NOTES, "_l10n_zh_extract.json")
BATCHES = ["_en_batch_misc.json", "_en_batch_help.json", "_en_batch_monster.json",
           "_en_batch_relic.json", "_en_batch_skill.json", "_en_batch_trap.json"]

rx_term = re.compile(r"\[\[([^\]]+)\]\]")
rx_token = re.compile(r"\{([^{}]+)\}")
rx_code = re.compile(r"\[([^\[\]]+)\]")
ICON_CODES = {"armor", "attack", "MHP", "HP", "adjacent", "money", "death", "action",
              "basic_armor"}

report = []

def load(path):
    with io.open(path, "r", encoding="utf-8") as f:
        return json.load(f)

# --- merge ---
entries = {}
for b in BATCHES:
    data = load(os.path.join(NOTES, b))
    for k, v in data.items():
        if k in entries:
            report.append("DUP-KEY: %s (in %s)" % (k, b))
        entries[k] = v

cards = {"schema": 1, "language": "en", "entries": entries}
with io.open(CARDS_OUT, "w", encoding="utf-8") as f:
    json.dump(cards, f, ensure_ascii=False, indent=2)
report.append("merged cards.json entries: %d" % len(entries))

# --- check 1: parse three JSONs ---
ok1 = True
for p in (GLOSSARY, CARDS_OUT, EXTRACT):
    try:
        load(p)
    except Exception as e:
        ok1 = False
        report.append("PARSE-FAIL: %s -> %s" % (p, e))
report.append("check1 parse: %s" % ("PASS" if ok1 else "FAIL"))

glossary = load(GLOSSARY)["entries"]
extract = load(EXTRACT)
zh_entries = extract["entries"]
en_names = set(v["displayName"] for v in glossary.values())

# --- check 2: coverage ---
missing = [k for k in zh_entries if k not in entries]
extra = [k for k in entries if k not in zh_entries]
report.append("check2 coverage: %s (missing=%d extra=%d)" %
              ("PASS" if not missing and not extra else "FAIL", len(missing), len(extra)))
for k in missing: report.append("  MISSING: " + k)
for k in extra: report.append("  EXTRA: " + k)

# field coverage: en fields vs zh fields
field_gaps = []
for k, zh in zh_entries.items():
    en = entries.get(k) or {}
    for field in ("displayName", "description", "faceIntro"):
        if zh.get(field) and not en.get(field):
            field_gaps.append("%s.%s" % (k, field))
        if en.get(field) and not zh.get(field):
            field_gaps.append("%s.%s (extra-en)" % (k, field))
report.append("field coverage gaps: %d" % len(field_gaps))
for g in field_gaps: report.append("  FIELD-GAP: " + g)

# --- check 3: [[X]] in en exist in glossary en displayNames ---
bad_terms = []
for k, en in entries.items():
    for field in ("displayName", "description", "faceIntro"):
        t = en.get(field)
        if not t: continue
        for m in rx_term.findall(t):
            if m.strip() not in en_names:
                bad_terms.append("%s.%s -> [[%s]]" % (k, field, m))
report.append("check3 terms: %s (bad=%d)" % ("PASS" if not bad_terms else "FAIL", len(bad_terms)))
for b in bad_terms: report.append("  BAD-TERM: " + b)

# --- check 4: {tokens} preserved verbatim (multiset per entry+field) ---
tok_fail = []
for k, zh in zh_entries.items():
    en = entries.get(k) or {}
    for field in ("displayName", "description", "faceIntro"):
        zt = sorted(rx_token.findall(zh.get(field) or ""))
        et = sorted(rx_token.findall(en.get(field) or ""))
        if zt != et:
            tok_fail.append("%s.%s zh=%s en=%s" % (k, field, zt, et))
report.append("check4 tokens: %s (mismatch=%d)" % ("PASS" if not tok_fail else "FAIL", len(tok_fail)))
for t in tok_fail: report.append("  TOKEN-MISMATCH: " + t)

# --- extra: [code] icon codes preserved (multiset, icon codes only) ---
code_fail = []
for k, zh in zh_entries.items():
    en = entries.get(k) or {}
    for field in ("displayName", "description", "faceIntro"):
        def icodes(s):
            s = rx_term.sub("", s or "")
            return sorted(c for c in rx_code.findall(s) if c in ICON_CODES)
        zc, ec = icodes(zh.get(field)), icodes(en.get(field))
        if zc != ec:
            code_fail.append("%s.%s zh=%s en=%s" % (k, field, zc, ec))
report.append("extra icon-codes: %s (mismatch=%d)" % ("PASS" if not code_fail else "FAIL", len(code_fail)))
for c in code_fail: report.append("  CODE-MISMATCH: " + c)

# --- extra: glossary skill-name terms match cards.json skill displayNames ---
TERM2SKILL = {
 "吸收":"skill.absorb","献火":"skill.offer_fire","剧烈燃烧":"skill.intense_burning",
 "损耗":"skill.attrition","快递":"skill.delivery","历战":"skill.battle_hardened",
 "嘲讽":"skill.taunt","叠甲":"skill.stack_armor","天涯若比邻":"skill.world_as_neighbors",
 "刺客领袖":"skill.assassin_leader","死亡之主":"skill.lord_of_death","烈焰沸腾":"skill.flame_boiling",
 "吞云吐雾":"skill.cloud_breath","生生不息":"skill.endless_flame","空间掌握":"skill.space_mastery",
 "转动":"skill.turn_world","献身":"skill.sacrifice","跳杀":"skill.leap_kill","提速":"skill.speed_up",
 "链接战术":"skill.link_tactics","链接准备":"skill.link_prep","链接护甲":"skill.link_armor",
 "盗取":"skill.steal","呼唤":"skill.call_melee6","死亡召唤":"skill.death_summon","吟唱":"skill.chant",
 "休养":"skill.recuperate","逃避":"skill.evade","起来":"skill.rise_up","远程武器":"skill.ranged_weapon",
 "神圣决斗":"skill.holy_duel","潜伏近战":"skill.ambush_melee"}
gs_fail = []
for zh_term, sid in TERM2SKILL.items():
    g = glossary.get(zh_term, {}).get("displayName")
    s = (entries.get(sid) or {}).get("displayName")
    if g != s:
        gs_fail.append("%s: glossary=%r skill(%s)=%r" % (zh_term, g, sid, s))
report.append("extra glossary-skill sync: %s (mismatch=%d)" % ("PASS" if not gs_fail else "FAIL", len(gs_fail)))
for g in gs_fail: report.append("  GS-MISMATCH: " + g)

# --- check 5: description length > 34 (token/code/term each count 1 unit) ---
def units(s):
    n = 0; i = 0
    while i < len(s):
        if s.startswith("[[", i):
            j = s.find("]]", i)
            if j >= 0: n += 1; i = j + 2; continue
        if s[i] == "[":
            j = s.find("]", i)
            if j >= 0: n += 1; i = j + 1; continue
        if s[i] == "{":
            j = s.find("}", i)
            if j >= 0: n += 1; i = j + 1; continue
        n += 1; i += 1
    return n

over = []
kind_over = {}
kind_total = {}
for k, en in entries.items():
    d = en.get("description")
    if d:
        kind = (zh_entries.get(k) or {}).get("kind", "?")
        kind_total[kind] = kind_total.get(kind, 0) + 1
        u = units(d)
        if u > 34:
            over.append((u, k))
            kind_over[kind] = kind_over.get(kind, 0) + 1
over.sort(reverse=True)
report.append("check5 over-34-unit descriptions: %d / %d" %
              (len(over), sum(1 for e in entries.values() if e.get("description"))))
report.append("  by kind: " + ", ".join("%s %d/%d" % (kk, kind_over.get(kk, 0), kind_total[kk])
                                          for kk in sorted(kind_total)))
for u, k in over: report.append("  OVER34: %3d  %s" % (u, k))

out = "\n".join(report)
with io.open(os.path.join(NOTES, "_l10n_check_report.txt"), "w", encoding="utf-8") as f:
    f.write(out + "\n")
print(out[:3000])

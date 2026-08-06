import json, io

tpl = json.load(io.open('Assets/StreamingAssets/ContentVisual/tables/effect_templates.json', encoding='utf-8'))
byid = {e['id']: e for e in tpl}
for tid in ['tpl.relic.terror_mask.remove', 'tpl.trap.flame.remove', 'tpl.trap.revive_stone.interact',
            'tpl.trap.rolling_stone.slot3', 'tpl.help.kidnapping.use']:
    e = byid.get(tid)
    if not e:
        continue
    print('===', tid)
    print('requires :', e.get('requires_json'))
    print('conds    :', e.get('conditions_json'))
    print('body     :', e.get('body'))
    print()

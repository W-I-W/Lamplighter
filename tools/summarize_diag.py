"""Summarize user://diagnostics_log.jsonl (Windows: %APPDATA%\Godot\app_userdata\Lanther\ or Lamplighter) into milestones."""
import json, sys, glob, os
p = sys.argv[1] if len(sys.argv) > 1 else glob.glob(os.path.expandvars(r'%APPDATA%\Godot\app_userdata\*\diagnostics_log.jsonl'))[0]
rows = [json.loads(l) for l in open(p, encoding='utf-8') if l.strip()]
for mins in (15, 60, 180, 300, 600):
    r = [x for x in rows if x['play_secs'] >= mins * 60]
    if not r: print(f'{mins:>4} min: not reached'); continue
    x = r[0]
    print(f"{mins:>4} min: lvl {x['level']} lit {x['lit']} beacon {x['beacon']} bosses {x['bosses']} buildings {x['buildings']} villagers {x['villagers']} "
          f"mining {x['mine_secs']/60:.0f}m dark {x['dark_secs']/60:.0f}m deaths {x['deaths']} travel {x['fast_travel']}")

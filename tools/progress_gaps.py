"""Find 'reward gaps' in the first minutes of play from the local diagnostics (Settings -> Diagnostics, or LAMP_DIAG=1).

Usage:  python tools/progress_gaps.py [dir_with_diagnostics] [--minutes 15] [--gap 90]

Two views of the same session:
  1. first_events_secs (diagnostics.json): moments of the first lantern, first spend, first tool upgrade, ...  Long pauses between them are gaps.
  2. diagnostics_log.jsonl (one row per minute): minutes where nothing grew (level, tier, lit, buildings, villagers, relics, bosses) are 'quiet minutes'.
Note: session_secs in first_events is time since the session started; play_secs in the log is time in the world. They are close in a fresh game.
"""
import argparse, glob, json, os, sys

ap = argparse.ArgumentParser()
ap.add_argument('dir', nargs='?', default=None)
ap.add_argument('--minutes', type=float, default=15)
ap.add_argument('--gap', type=float, default=90, help='seconds without a new milestone that counts as a gap')
a = ap.parse_args()

if a.dir is None:
    found = glob.glob(os.path.expandvars(r'%APPDATA%\Godot\app_userdata\*\diagnostics.json'))
    if not found: sys.exit('diagnostics.json not found; pass the folder explicitly')
    a.dir = os.path.dirname(max(found, key=os.path.getmtime))
limit = a.minutes * 60

def fmt(s): return f'{int(s // 60)}:{int(s % 60):02d}'

# 1. milestones
dp = os.path.join(a.dir, 'diagnostics.json')
ev = {}
if os.path.exists(dp):
    ev = json.load(open(dp, encoding='utf-8')).get('first_events_secs', {})
marks = sorted((t, n) for n, t in ev.items() if t <= limit)
print(f'== Milestones in the first {a.minutes:g} min ({len(marks)}) ==')
prev_t, prev_n = 0.0, 'start'
gaps = []
for t, n in marks:
    flag = '  <-- GAP' if t - prev_t > a.gap else ''
    print(f'{fmt(t):>6}  {n}{flag}')
    if t - prev_t > a.gap: gaps.append((prev_t, t, prev_n, n))
    prev_t, prev_n = t, n
if limit - prev_t > a.gap and prev_t < limit and (not marks or marks[-1][0] < limit):
    gaps.append((prev_t, limit, prev_n, 'end of window'))
    print(f'{fmt(limit):>6}  (end of window)  <-- GAP')

print(f'\n== Gaps longer than {a.gap:g}s ==')
if not gaps: print('none')
for s, e, p, n in gaps: print(f'{fmt(s)}-{fmt(e)} ({e - s:.0f}s): nothing between "{p}" and "{n}"')

# 2. quiet minutes from the log
lp = os.path.join(a.dir, 'diagnostics_log.jsonl')
if os.path.exists(lp):
    rows = [json.loads(l) for l in open(lp, encoding='utf-8') if l.strip()]
    rows = [r for r in rows if r.get('play_secs', 0) <= limit + 60]
    keys = ('level', 'tier', 'lit', 'buildings', 'villagers', 'relics', 'bosses', 'beacon')
    print(f'\n== Quiet minutes (no growth in {", ".join(keys)}) ==')
    quiet = 0
    for p, c in zip(rows, rows[1:]):
        if all(c.get(k, 0) <= p.get(k, 0) for k in keys):
            quiet += 1; print(f'{fmt(p["play_secs"])}-{fmt(c["play_secs"])}: no change; deaths +{c.get("deaths", 0) - p.get("deaths", 0)}')
    if not quiet: print('none')
else:
    print('\n(diagnostics_log.jsonl not found: no minute-by-minute view)')

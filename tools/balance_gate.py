#!/usr/bin/env python3
"""Регрессионный шлюз баланса: сравнивает прогон `balance.ps1 check` с эталоном.

    python tools/balance_gate.py save [--run balance/out/current] [--baseline balance/baseline.json]
    python tools/balance_gate.py diff [--run ...] [--baseline ...] [--max-win-drop 0.05] [--max-hours-drift 0.25]
    python tools/balance_gate.py list            # доступные прогоны в balance/out

Что считается регрессией (код 1):
  * статус model_error / unverified, либо прогон без runtime_verified;
  * доля побед профиля изменилась и доверительные интервалы эталона и нового прогона НЕ пересекаются
    (или сдвиг больше --max-win-drop при пересечении не считается шумом);
  * медианное время прохождения сдвинулось больше чем на --max-hours-drift (доля);
  * появились новые замечания (по Area+профилю) уровня warn/error, которых не было в эталоне;
  * сменился seed или меньше прогонов, чем в эталоне — сравнение предупреждает, что оно неточное.
Улучшения (победы выросли в сторону цели, замечания исчезли) только выводятся.
Эталон — маленький JSON, его стоит коммитить (balance/baseline.json); сами прогоны лежат в gitignored balance/out/.
"""
import argparse, glob, json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)


def load(path):
    with open(path, encoding='utf-8') as f: return json.load(f)


def quality_of(run):
    p = run if run.endswith('.json') else os.path.join(run, 'quality.json')
    if not os.path.exists(p): sys.exit(f'[gate] нет {p}. Сначала: powershell -File tools\\balance.ps1 check --n 1000 --seed 20261106 --name current')
    return load(p)


def latest_run():
    runs = [os.path.dirname(p) for p in glob.glob('balance/out/*/quality.json')]
    runs.sort(key=lambda d: os.path.getmtime(os.path.join(d, 'quality.json')), reverse=True)
    return runs[0] if runs else 'balance/out/current'


def slim(q):
    return dict(
        schema=1, status=q.get('status'), seed=q.get('seed'), total_runs=q.get('TotalRuns'),
        runtime_verified=q.get('runtime_verified'), model_sha256=q.get('model_sha256'),
        profiles={p['Profile']: dict(n=p['N'], win=p['WinRate'], lo=p['WinLo'], hi=p['WinHi'],
                                     hours=p.get('median_hours'), deaths=p.get('DeathsMean'),
                                     prep_stops=p.get('PreparationStops'), attempt_stops=p.get('AttemptStops'))
                  for p in q.get('profiles', [])},
        findings=sorted({finding_key(f) for f in q.get('findings', []) if f.get('Severity') in ('warn', 'error')}),
    )


def finding_key(f):
    # профиль/сценарий + область; числа в тексте нестабильны, поэтому не включаем
    m = re.match(r'^([\w\-]+)[:\s]', f.get('Text', ''))
    return f"{f.get('Severity')}|{f.get('Area')}|{m.group(1) if m else ''}"


def cmd_list(_):
    for d in sorted(glob.glob('balance/out/*/quality.json'), key=os.path.getmtime, reverse=True):
        q = load(d)
        print(f"{os.path.dirname(d):55} {q.get('status'):15} runs={q.get('TotalRuns')} findings={len(q.get('findings', []))}")
    return 0


def cmd_save(a):
    run = a.run or latest_run(); q = quality_of(run)
    if q.get('status') in ('model_error', 'unverified') and not a.force:
        print(f"[gate] отказ: статус прогона {q.get('status')}; --force чтобы всё равно сохранить"); return 2
    os.makedirs(os.path.dirname(a.baseline) or '.', exist_ok=True)
    json.dump(slim(q), open(a.baseline, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(f'[gate] эталон сохранён: {a.baseline} (из {run}, runs={q.get("TotalRuns")}, seed={q.get("seed")})'); return 0


def cmd_diff(a):
    run = a.run or latest_run(); new = slim(quality_of(run))
    if not os.path.exists(a.baseline): print(f'[gate] нет эталона {a.baseline}: выполните `save`'); return 2
    old = load(a.baseline)
    bad, note, good = [], [], []
    print(f'[gate] прогон {run} против {a.baseline}')
    if new['status'] in ('model_error', 'unverified'): bad.append(f"статус прогона {new['status']}")
    if not new['runtime_verified']: bad.append('нет сверки с настоящим Game (runtime_verified=false)')
    if new['seed'] != old['seed']: note.append(f"seed отличается ({old['seed']} → {new['seed']}): различия могут быть шумом")
    if new['total_runs'] < old['total_runs']: note.append(f"прогонов меньше эталона ({old['total_runs']} → {new['total_runs']}): интервалы шире")
    if new['model_sha256'] != old['model_sha256']: note.append('снимок чисел игры (model_sha256) изменился — это ожидаемо после правок баланса')

    print(f"\n{'профиль':10} {'победы было→стало':>22} {'часы было→стало':>18} {'смерти':>12}")
    for name, o in old['profiles'].items():
        n = new['profiles'].get(name)
        if not n: bad.append(f'профиль {name} пропал из прогона'); continue
        dw = n['win'] - o['win']
        disjoint = n['lo'] > o['hi'] or n['hi'] < o['lo']
        line = f"{name:10} {o['win']*100:6.1f}% → {n['win']*100:6.1f}% ({dw*100:+5.1f}) "
        hrs = ''
        if o['hours'] and n['hours']:
            drift = (n['hours'] - o['hours']) / o['hours']
            hrs = f"{o['hours']:5.2f}→{n['hours']:5.2f}ч ({drift*100:+4.0f}%)"
            if abs(drift) > a.max_hours_drift: bad.append(f"{name}: медианное время {o['hours']:.2f}→{n['hours']:.2f} ч ({drift*100:+.0f}%)")
        elif bool(o['hours']) != bool(n['hours']):
            hrs = f"{o['hours']}→{n['hours']}"; bad.append(f"{name}: появилось/пропало время прохождения (победы {o['win']:.3f}→{n['win']:.3f})")
        print(f"{line:34} {hrs:>18} {o['deaths']!s:>6}→{n['deaths']!s:<6}")
        if disjoint and abs(dw) >= a.min_win_shift:
            (bad if dw < 0 or a.strict_up else good).append(f"{name}: победы {o['win']*100:.1f}% → {n['win']*100:.1f}% (интервалы не пересекаются)")
        elif abs(dw) > a.max_win_drop: note.append(f"{name}: победы {dw*100:+.1f} п.п., но интервалы пересекаются (шум?)")

    of, nf = set(old['findings']), set(new['findings'])
    for k in sorted(nf - of): bad.append(f'новое замечание: {k}')
    for k in sorted(of - nf): good.append(f'замечание устранено: {k}')

    for t, items in (('РЕГРЕССИИ', bad), ('УЛУЧШЕНИЯ / ИЗМЕНЕНИЯ', good), ('ЗАМЕЧАНИЯ О СРАВНЕНИИ', note)):
        if items:
            print(f'\n{t}:')
            for i in items: print('  -', i)
    print('\n[gate]', 'FAIL' if bad else 'OK', f'({len(bad)} регрессий)')
    return 1 if bad else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('cmd', choices=['save', 'diff', 'list'])
    ap.add_argument('--run'); ap.add_argument('--baseline', default='balance/baseline.json')
    ap.add_argument('--max-win-drop', type=float, default=0.05); ap.add_argument('--min-win-shift', type=float, default=0.02)
    ap.add_argument('--max-hours-drift', type=float, default=0.25)
    ap.add_argument('--strict-up', action='store_true', help='считать значимый РОСТ побед тоже регрессией')
    ap.add_argument('--force', action='store_true')
    a = ap.parse_args()
    return dict(save=cmd_save, diff=cmd_diff, list=cmd_list)[a.cmd](a)


if __name__ == '__main__':
    sys.exit(main())

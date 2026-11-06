#!/usr/bin/env python3
"""Быстрый статический линтер проекта Lanternreach. Godot не запускается (секунды).

    python tools/lint.py            # все проверки, коды: 0 ок, 1 есть ошибки
    python tools/lint.py --strict   # предупреждения тоже считаются ошибками
    python tools/lint.py --only res,loc --json out.json

Проверки (--only): json, res, uid, eol, loc, stages, docs, junk, balance
"""
import argparse, glob, json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(ROOT)
CYR = re.compile('[А-Яа-яЁё]')
SKIP_DIRS = ('.git', '.godot', '.patch', '.vs', 'build', 'addons', 'balance/out', 'obj', 'bin')
issues = []  # (severity, check, where, text)


def rel(p): return p.replace('\\', '/')
def add(sev, check, where, text): issues.append((sev, check, rel(where), text))


def walk(*exts):
    for dp, dn, fn in os.walk('.'):
        d = rel(dp)[2:]
        dn[:] = [x for x in dn if not any(rel(os.path.join(d, x)).lstrip('/') == s or x == s for s in SKIP_DIRS)]
        for f in fn:
            if f.endswith(exts): yield rel(os.path.join(dp, f))[2:]


def read(p):
    with open(p, encoding='utf-8', errors='replace', newline='') as f: return f.read()


# ---------- json ----------
def check_json():
    files = ['lang/en.json', 'balance/targets.json', 'balance/profiles.json'] + glob.glob('balance/proposals/*.json')
    for p in files:
        if not os.path.exists(p): continue
        try: json.loads(read(p))
        except Exception as e: add('error', 'json', p, f'невалидный JSON: {e}')
    try:
        t = json.loads(read('balance/targets.json'))
        for name, pr in t.get('Profiles', {}).items():
            if pr['WinMin'] > pr['WinMax']: add('error', 'json', 'balance/targets.json', f'{name}: WinMin > WinMax')
            if pr['HoursMin'] > pr['HoursMax']: add('error', 'json', 'balance/targets.json', f'{name}: HoursMin > HoursMax')
            for ms, (lo, hi) in pr.get('Milestones', {}).items():
                if lo > hi: add('error', 'json', 'balance/targets.json', f'{name}.{ms}: min > max')
    except Exception: pass


# ---------- res:// ----------
RES = re.compile(r'res://([A-Za-z0-9_./ \-]+?)(?=["\'\]\)\s,;}]|$)')
def check_res():
    files = list(walk('.tscn', '.tres', '.cs', '.gd', '.godot', '.cfg'))
    files.append('project.godot')
    refd = set()
    for p in files:
        if os.path.exists(p): refd.update(m.group(1) for m in RES.finditer(read(p)))
    for p in files:
        if not os.path.exists(p): continue
        for ln, line in enumerate(read(p).split('\n'), 1):
            if line.lstrip().startswith('//'): continue
            for m in RES.finditer(line):
                target = m.group(1).rstrip('/')
                if not target or target.endswith(('/',)): continue
                # путь-префикс директории (например "Look/" + name) пропускаем
                if line[m.end():m.end() + 1] in ('"',) and False: continue
                if os.path.exists(target): continue
                if '{' in target or '*' in target: continue
                if os.path.exists(target + '.import') or os.path.isdir(target): continue
                # директория-префикс в C# (конкатенация)
                if p.endswith('.cs') and re.search(re.escape(m.group(0)) + r'["\']\s*\+', line): continue
                if p.endswith('.cs') and '$"' in line and '{' in line[m.end():]: continue
                orphan = p.endswith(('.tscn', '.tres')) and p not in refd
                add('warn' if orphan else 'error', 'res', f'{p}:{ln}', f'нет файла res://{target}' + (' (сцена нигде не используется)' if orphan else ''))
    # [ext_resource ... uid="uid://..."] без совпадающего .uid не проверяем — Godot чинит сам


# ---------- .uid ----------
def check_uid():
    for p in walk('.cs'):
        if p.startswith('Scripts/Tests/') and False: continue
        if not os.path.exists(p + '.uid'): add('warn', 'uid', p, 'нет .cs.uid (откройте проект в редакторе Godot)')
    for p in walk('.uid'):
        if not os.path.exists(p[:-4]): add('warn', 'uid', p, 'осиротевший .uid без исходника')


# ---------- eol / merge markers ----------
def check_eol():
    for p in walk('.cs', '.tscn', '.tres', '.json', '.md', '.ps1', '.py', '.gdshader'):
        t = read(p)
        crlf = t.count('\r\n'); lf = t.count('\n') - crlf
        if crlf and lf and min(crlf, lf) > 3 and p.endswith(('.cs', '.tscn', '.tres')):
            add('warn', 'eol', p, f'смешанные переводы строк (CRLF {crlf}, LF {lf})')
        if re.search(r'^(<<<<<<< |>>>>>>> )', t, re.M): add('error', 'eol', p, 'маркеры слияния git')
        if t.startswith('﻿') and p.endswith('.cs'): pass


# ---------- localization ----------
LIT = re.compile(r'(\$?)"((?:[^"\\\n]|\\.)*)"')
def convert(s):
    n, res, i = 0, [], 0
    while i < len(s):
        c = s[i]
        if c == '{':
            if i + 1 < len(s) and s[i + 1] == '{': res.append('{'); i += 2; continue
            depth, j = 1, i + 1
            while j < len(s) and depth:
                depth += (s[j] == '{') - (s[j] == '}'); j += 1
            res.append('{%d}' % n); n += 1; i = j; continue
        if c == '}' and s[i + 1:i + 2] == '}': res.append('}'); i += 2; continue
        res.append(c); i += 1
    return ''.join(res)


def code_strings():
    out = {}
    for p in walk('.cs'):
        if p.startswith('Scripts/Tests/') or p.startswith('Scripts/Balance/') or p.startswith('Scripts/Debug/') or p.endswith('Loc.cs'): continue
        for ln, line in enumerate(read(p).split('\n'), 1):
            s = line.strip()
            if s.startswith('//') or s.startswith('///'): continue
            line = re.sub(r'\s//.*$', '', line) if '"' not in line.split('//')[-1] else line
            for m in LIT.finditer(line):
                body = m.group(2)
                if not CYR.search(body): continue
                body = body.replace('\\"', '"').replace('\\n', '\n').replace('\\\\', '\\')
                out.setdefault(convert(body) if m.group(1) else body, f'{p}:{ln}')
    return out


RUN = re.compile(r"[А-Яа-яЁё][А-Яа-яЁё .,'«»…—\-:!?]*[А-Яа-яЁё.»]|[А-Яа-яЁё]")
BULLET = re.compile(r'^((?:[▶•★☆✓●◆○▮ -]+|\[[^\]]{1,2}\] ))(.+)$', re.S)


def make_translator(en):
    exact = {k for k in en if not re.search(r'\{\d+\}', k)}
    pats = [re.compile('^' + re.sub(r'\\{\d+\\}', '(.+?)', re.escape(k)) + '$', re.S) for k in en if re.search(r'\{\d+\}', k)]
    suf = [k for k in exact if len(k) > 2 and k[0] in ' ,']
    pre = [k for k in exact if len(k) > 2 and k[-1] in ' :' and k not in suf]

    def ok(s, depth=0):
        if not CYR.search(s) or depth > 6: return True
        if s in exact: return True
        for r in pats:
            m = r.match(s)
            if m: return all(ok(g, depth + 1) for g in m.groups())
        m = BULLET.match(s)
        if m: return ok(m.group(2), depth + 1)
        for x in suf:
            if len(s) > len(x) and s.endswith(x): return ok(s[:-len(x)], depth + 1)
        for x in pre:
            if len(s) > len(x) and s.startswith(x): return ok(s[len(x):], depth + 1)
        runs = RUN.findall(s)
        return len(runs) > 0 and not (len(runs) == 1 and runs[0] == s) and all(r in exact or ok(r, depth + 1) for r in runs if r != s)
    return ok


def check_loc():
    try: en = json.loads(read('lang/en.json'))
    except Exception: return
    code = code_strings()
    ok = make_translator(en)
    missing = [k for k in code if not ok(k)]
    for k in missing[:200]:
        add('warn', 'loc', code[k], f'нет английского перевода: {k[:70]!r}')
    if len(missing) > 200: add('warn', 'loc', 'lang/en.json', f'… и ещё {len(missing) - 200} без перевода')
    for k, v in en.items():
        if not isinstance(v, str) or not v.strip():
            add('warn', 'loc', 'lang/en.json', f'пустой перевод: {k[:60]!r}'); continue
        pk = sorted(re.findall(r'\{\d+\}', k)); pv = sorted(re.findall(r'\{\d+\}', v))
        if pk != pv: add('error', 'loc', 'lang/en.json', f'не совпадают плейсхолдеры {pk} vs {pv}: {k[:60]!r}')
        if CYR.search(v): add('warn', 'loc', 'lang/en.json', f'в английском значении осталась кириллица: {k[:60]!r}')
    unused = [k for k in en if k not in code]
    if unused:  # информативно: часть ключей приходит из .tres/динамики
        add('info', 'loc', 'lang/en.json', f'ключей без литерала в коде: {len(unused)} (могут браться из ресурсов)')


# ---------- test stages <-> run_checks.ps1 ----------
def check_stages():
    try:
        game = ''.join(read(p) for p in glob.glob('Scripts/Tests/*.cs'))
        runner = read('tools/run_checks.ps1')
    except Exception: return
    stages = set(re.findall(r'LAMP_STAGE"\)\s*==\s*"([a-z0-9_]+)"', game))
    # стадии, которые вспомогательные/ручные
    manual = {'genres', 'ash', 's4', 's4b', 'steamprobe', 'veg', 'riftbal', 'pressure', 'mksave', 'savesmoke', 'nethost', 'netjoin', 'netflowhost', 'netflowjoin', 'interactload', 'reload'}
    used = set(re.findall(r"Run-Stage\s+'[^']*'\s+'([a-z0-9_]+)'", runner)) | set(re.findall(r"'([a-z0-9_]+)'", runner))
    for s in sorted(stages - used - manual):
        add('warn', 'stages', 'tools/run_checks.ps1', f'тестовая стадия «{s}» не вызывается из run_checks.ps1')
    for s in sorted(set(re.findall(r"Run-Stage\s+'[^']*'\s+'([a-z0-9_]+)'", runner)) - stages):
        add('error', 'stages', 'tools/run_checks.ps1', f'run_checks.ps1 вызывает несуществующую стадию «{s}»')


# ---------- docs links ----------
def check_docs():
    link = re.compile(r'\]\(([^)#\s]+)(?:#[^)]*)?\)')
    for p in walk('.md'):
        base = os.path.dirname(p)
        for ln, line in enumerate(read(p).split('\n'), 1):
            for m in link.finditer(line):
                t = m.group(1)
                if re.match(r'^[a-z]+:', t): continue
                if not os.path.exists(os.path.normpath(os.path.join(base, t))):
                    add('warn', 'docs', f'{p}:{ln}', f'битая ссылка {t}')


# ---------- junk ----------
def check_junk():
    for p in walk('.bak', '.orig', '.rej', '.tmp'):
        add('warn', 'junk', p, 'резервная/временная копия в репозитории')
    for p in glob.glob('*.wav'):
        if not any(os.path.basename(p) in read(q) for q in walk('.cs', '.tres', '.tscn')):
            add('warn', 'junk', p, 'аудиофайл в корне проекта не используется нигде в коде/ресурсах')
    if os.path.isdir('balance/out') and not os.path.exists('balance/out/.gdignore'):
        add('warn', 'junk', 'balance/out', 'нет .gdignore — Godot импортирует CSV отчётов в .translation (создайте пустой balance/out/.gdignore)')


# ---------- balance data sanity (без симуляции) ----------
def check_balance():
    p = 'Resources/Combat/tuning.tres'
    if not os.path.exists(p): return
    for ln, line in enumerate(read(p).split('\n'), 1):
        m = re.match(r'^([A-Za-z0-9_]+)\s*=\s*(-?[0-9.eE+\-]+)\s*$', line.strip())
        if not m: continue
        name, v = m.group(1), float(m.group(2))
        if v != v or abs(v) > 1e9: add('error', 'balance', f'{p}:{ln}', f'{name}={v}')
        if v < 0 and not re.search(r'(offset|bias|delta|min)', name, re.I):
            add('warn', 'balance', f'{p}:{ln}', f'отрицательный параметр {name}={v}')
    # у «Resources/Skills» цены должны расти с уровнем
    for p in glob.glob('Resources/Skills/*.tres'):
        t = read(p)
        m = re.search(r'costs\s*=\s*(?:Packed\w+Array\()?\[?([0-9, .\-]+)\]?\)?', t)
        if m:
            try:
                xs = [float(x) for x in m.group(1).split(',') if x.strip()]
                if any(b < a for a, b in zip(xs, xs[1:])): add('warn', 'balance', p, f'цены уровней не растут: {xs}')
            except ValueError: pass


CHECKS = dict(json=check_json, res=check_res, uid=check_uid, eol=check_eol, loc=check_loc,
              stages=check_stages, docs=check_docs, junk=check_junk, balance=check_balance)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--only', default=','.join(CHECKS)); ap.add_argument('--strict', action='store_true')
    ap.add_argument('--json'); ap.add_argument('--max', type=int, default=25, help='строк на проверку')
    a = ap.parse_args()
    for name in a.only.split(','):
        if name not in CHECKS: print(f'неизвестная проверка {name}; доступны: {", ".join(CHECKS)}'); return 2
        CHECKS[name]()
    err = sum(i[0] == 'error' for i in issues); warn = sum(i[0] == 'warn' for i in issues)
    for name in a.only.split(','):
        mine = [i for i in issues if i[1] == name]
        tag = 'FAIL' if any(i[0] == 'error' for i in mine) else ('WARN' if any(i[0] == 'warn' for i in mine) else 'ok  ')
        print(f'[{tag}] {name}: {sum(i[0]=="error" for i in mine)} ошибок, {sum(i[0]=="warn" for i in mine)} предупреждений')
        for sev, _, where, text in mine[:a.max]:
            if sev != 'info' or True: print(f'    {sev:5} {where}: {text}')
        if len(mine) > a.max: print(f'    … ещё {len(mine) - a.max}')
    if a.json:
        json.dump([dict(severity=s, check=c, where=w, text=t) for s, c, w, t in issues], open(a.json, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(f'LINT: {err} ошибок, {warn} предупреждений')
    return 1 if err or (a.strict and warn) else 0


if __name__ == '__main__':
    sys.exit(main())

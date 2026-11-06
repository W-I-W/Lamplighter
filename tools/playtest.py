#!/usr/bin/env python3
"""Ручная проверка баланса по реальным сессиям (телеметрия Diag, включается в настройках или LAMP_DIAG=1).

    python tools/playtest.py report [LOG ...] [--profile novice|average|expert] [--md out.md]
    python tools/playtest.py checklist [--out playtest_checklist.md]

report: берёт diagnostics_log.jsonl (по умолчанию — все найденные в %APPDATA%\\Godot\\app_userdata\\*),
  печатает контрольные точки (15 мин / 1 ч / 3 ч / 5 ч / 10 ч), сравнивает с критериями PLAYTEST.md
  и с целями balance/targets.json (смертей в час, время первого босса) -> OK / ПРОВЕРИТЬ.
  Рядом с логом читает diagnostics.json: первые события (первый фонарь < 8 мин, первая трата < 4 мин), fps.
checklist: чек-лист ручной проверки проекта и опросник игрока (Markdown, с галочками).
Код возврата report: 0 всё в норме, 1 есть пункты «ПРОВЕРИТЬ», 2 нет данных.
"""
import argparse, glob, json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
POINTS = (15, 60, 180, 300, 600)


def find_logs():
    pat = os.path.expandvars(r'%APPDATA%\Godot\app_userdata\*\diagnostics_log.jsonl')
    return sorted(glob.glob(pat), key=os.path.getmtime, reverse=True)


def load_rows(p):
    rows = []
    for l in open(p, encoding='utf-8'):
        l = l.strip()
        if not l: continue
        try: rows.append(json.loads(l))
        except ValueError: pass
    return rows


def at(rows, mins):
    r = [x for x in rows if x['play_secs'] >= mins * 60]
    return r[0] if r else None


def verdicts(rows, summ, targets, profile):
    """список (статус, критерий, факт). статус: ok / check / na"""
    v = []
    def put(ok, name, fact): v.append(('na' if ok is None else 'ok' if ok else 'check', name, fact))
    first = (summ or {}).get('first_events_secs', {})
    for key, lim, label in (('first_lantern', 8, 'первый фонарь < 8 мин'), ('first_spend', 4, 'первая трата < 4 мин')):
        s = first.get(key)
        put(None if s is None else s / 60 < lim, label, 'нет данных' if s is None else f'{s / 60:.1f} мин')
    r15 = at(rows, 15)
    if r15:
        put(r15['deaths'] <= 2, 'за 15 мин смертей ≤ 2', str(r15['deaths']))
        put(r15['lit'] >= 1 and r15['buildings'] >= 1, 'за 15 мин ≥ 1 фонарь и постройка', f"фонарей {r15['lit']}, построек {r15['buildings']}")
    r60 = at(rows, 60)
    if r60:
        put(r60['lit'] >= 6, 'за час ≥ 6 фонарей', str(r60['lit']))
        put(r60['buildings'] >= 4, 'за час ≥ 4 построек', str(r60['buildings']))
        put(r60['mine_secs'] < 0.4 * r60['play_secs'], 'за час добыча < 40% времени', f"{100 * r60['mine_secs'] / max(1, r60['play_secs']):.0f}%")
    r180 = at(rows, 180)
    if r180:
        put(r180['beacon'] >= 3, 'за 3 часа маяк ≥ 3', str(r180['beacon']))
    # динамика добычи: доля mine_secs в последнем часе не больше, чем в первом
    if len(rows) > 2 and rows[-1]['play_secs'] > 2 * 3600:
        a = at(rows, 60); b = rows[-1]
        h_first = a['mine_secs'] / max(1, a['play_secs'])
        d_secs = b['play_secs'] - rows[max(0, len(rows) - 61)]['play_secs']
        d_mine = b['mine_secs'] - rows[max(0, len(rows) - 61)]['mine_secs']
        put(d_mine / max(1, d_secs) <= h_first, 'доля добычи не растёт к концу', f"{h_first * 100:.0f}% → {100 * d_mine / max(1, d_secs):.0f}%")
    last = rows[-1]
    hrs = last['play_secs'] / 3600
    pt = targets['Profiles'].get(profile) if targets else None
    if pt and hrs >= 1:
        dph = last['deaths'] / hrs
        put(dph <= pt['DeathsPerHourMax'], f'смертей/час ≤ {pt["DeathsPerHourMax"]} ({profile})', f'{dph:.1f}')
        b1 = next((x for x in rows if x['bosses'] >= 1), None)
        lo, hi = pt['Milestones']['boss1']
        if b1: put(lo <= b1['play_secs'] / 3600 <= hi, f'первый босс в {lo}–{hi} ч ({profile})', f"{b1['play_secs'] / 3600:.2f} ч")
        else: put(None if hrs < hi else False, f'первый босс в {lo}–{hi} ч ({profile})', 'ещё не побеждён')
    fmin = (summ or {}).get('fps_min')
    if fmin is not None: put(fmin >= 30, 'fps не ниже 30', f"min {fmin:.0f}, avg {(summ or {}).get('fps_avg', 0):.0f}")
    gaps = [(rows[i + 1]['play_secs'] - rows[i]['play_secs']) for i in range(len(rows) - 1)]
    stall = 0
    for i in range(1, len(rows)):
        j = i
        while j > 0 and rows[j]['lit'] == rows[i]['lit'] and rows[j]['bosses'] == rows[i]['bosses'] and rows[j]['tier'] == rows[i]['tier'] and rows[j]['level'] == rows[i]['level']: j -= 1
        stall = max(stall, rows[i]['play_secs'] - rows[j]['play_secs'])
    put(stall < 45 * 60, 'нет простоя без нового уровня/фонаря/босса/тира > 45 мин', f'макс. {stall / 60:.0f} мин')
    return v


def cmd_report(a):
    logs = a.logs or find_logs()
    if not logs: print('нет diagnostics_log.jsonl: включите «Диагностика» в настройках и сыграйте'); return 2
    targets = json.load(open(os.path.join(ROOT, 'balance/targets.json'), encoding='utf-8')) if os.path.exists(os.path.join(ROOT, 'balance/targets.json')) else None
    out, bad = [], 0
    for p in logs[:a.max]:
        rows = load_rows(p)
        if not rows: continue
        sp = os.path.join(os.path.dirname(p), 'diagnostics.json')
        summ = json.load(open(sp, encoding='utf-8')) if os.path.exists(sp) else None
        out.append(f'## {p}\nСессия {rows[0]["at"]} … {rows[-1]["at"]}, игровое время {rows[-1]["play_secs"] / 3600:.2f} ч\n')
        out.append('| точка | ур. | фонарей | маяк | боссов | построек | жителей | добыча | тьма | смертей | быстр.пер. |\n|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|')
        for m in POINTS:
            x = at(rows, m)
            out.append(f'| {m} мин | ' + (' | '.join(map(str, [x['level'], x['lit'], x['beacon'], x['bosses'], x['buildings'], x['villagers'], f"{x['mine_secs'] / 60:.0f}м", f"{x['dark_secs'] / 60:.0f}м", x['deaths'], x['fast_travel']])) if x else 'не достигнута' + ' |' * 10) + ' |')
        out.append('\n| | критерий | факт |\n|---|---|---|')
        for st, name, fact in verdicts(rows, summ, targets, a.profile):
            out.append(f"| {'✅' if st == 'ok' else '⚠️ ПРОВЕРИТЬ' if st == 'check' else '—'} | {name} | {fact} |")
            bad += st == 'check'
        out.append('')
    if not out: print('логи пусты'); return 2
    out.append(f'Итого к проверке: {bad}. Сверяйте с ответами игрока (`checklist`): менять баланс только там, где метрики и ощущения совпали.')
    text = '\n'.join(out)
    print(text)
    if a.md: open(a.md, 'w', encoding='utf-8').write(text)
    return 1 if bad else 0


CHECKLIST = """# Ручная проверка Lanternreach

Дата: ____   Сборка/коммит: ____   Игрок: ____   Профиль (novice/average/expert): ____

## A. Подготовка
- [ ] Настройки → «Диагностика» включена (или `LAMP_DIAG=1`)
- [ ] `python tools/lint.py` без ошибок; `tools\\check.ps1 quick` проходит
- [ ] Новый мир, чистый герой

## B. Первые 15 минут (без подсказок)
- [ ] Игрок сам нашёл первый фонарь (< 8 мин)  ·  Первая трата ресурсов (< 4 мин)
- [ ] Построена печь, зажжён ≥ 1 фонарь, смертей ≤ 2
- [ ] Игрок называет следующую цель сам: ____________________

## C. Первый час
- [ ] ≥ 6 фонарей, ≥ 4 типов построек, бой с первым стражем
- [ ] Появились новые способы добычи/крафта и игрок их заметил
- [ ] Добыча < 40% времени

## D. Бой и боссы (на каждого босса отдельно)
| Босс | Попыток | Смертей | Понятен ли паттерн | Несправедливые удары | Награда стоила? |
|---|--:|--:|---|---|---|
| Роща (Grove) | | | | | |
| Босс 2 | | | | | |
| Босс 3 | | | | | |
| Босс 4 (Зимний страж) | | | | | |
| Источник / финал | | | | | |
- [ ] Фазы босса читаются по полосе фаз; предупреждения успеваешь заметить
- [ ] Герою хватает лечения, но оно не делает бой тривиальным

## E. Экономика и развитие
- [ ] Навыки: каждый уровень ощутим (+30% света и т.п.), нет «мёртвых» навыков
- [ ] Оружие/вставки: есть минимум 2 разумных билда, нет одного очевидного
- [ ] Реликвии: какая ни разу не влияла? ____  Какая ломает игру? ____
- [ ] Улучшение предметов у печи/кузницы работает, цены адекватны
- [ ] Деревня: жители, профессии, общий сундук окупаются
- [ ] Нет цикла «бесконечных» ресурсов (обмен, продажа, разборка)

## F. Темп и поздняя игра
- [ ] Нет простоев > 45 мин без нового уровня/фонаря/босса/тира
- [ ] Автоматизация вытесняет ручную добычу час за часом
- [ ] Разломы, легенды, декор используются; финал достижим

## G. Техника
- [ ] Загрузка старого сохранения; сохранение/выход/вход
- [ ] Английская локализация: нет русских строк в интерфейсе
- [ ] Кооператив (если проверяется): хост + гость, босс, передача предметов
- [ ] fps ≥ 30 в густом лесу и при 30 врагах; нет вылетов и красных ошибок в консоли

## H. Опрос игрока
1. Где было скучно? ____
2. Что было непонятно? ____
3. Какой предмет/реликвия ни разу не пригодились? ____
4. Что показалось нечестным (смерти, удары боссов)? ____
5. Хочется ли играть дальше? Почему? ____

## I. Итог
После сессии: `python tools/playtest.py report --profile average --md playtest_report.md`
Расхождения метрик и ответов → записать в docs/BALANCE_PLAYTEST_<дата>.md, затем прогнать симулятор:
`powershell -File tools\\balance.ps1 check --n 1000 --seed 20261106 --name after`
"""


def cmd_checklist(a):
    open(a.out, 'w', encoding='utf-8').write(CHECKLIST); print(f'чек-лист записан: {a.out}'); return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sp = ap.add_subparsers(dest='cmd', required=True)
    r = sp.add_parser('report'); r.add_argument('logs', nargs='*'); r.add_argument('--profile', default='average', choices=['novice', 'average', 'expert', 'optimizer']); r.add_argument('--md'); r.add_argument('--max', type=int, default=3)
    c = sp.add_parser('checklist'); c.add_argument('--out', default='playtest_checklist.md')
    a = ap.parse_args()
    return cmd_report(a) if a.cmd == 'report' else cmd_checklist(a)


if __name__ == '__main__':
    sys.exit(main())

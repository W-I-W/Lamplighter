# Инструменты проверки проекта

| Команда | Время | Что делает |
|---|---|---|
| `python tools/lint.py [--strict] [--only json,res,loc,...]` | ~1 с | статический линтер без Godot: JSON и `targets.json`, ссылки `res://`, `.uid`, переводы строк и маркеры слияния, покрытие `lang/en.json` (с учётом шаблонов и префиксов `Loc.T`), плейсхолдеры, стадии `LAMP_STAGE` и `run_checks.ps1`, ссылки в `docs/`, мусор в корне, `tuning.tres` |
| `powershell -File tools\check.ps1 lint` | ~1 с | то же через единый вход |
| `... check.ps1 quick` | ~5 мин | lint + `dotnet build` + сверка баланс-модели с настоящим `Game` |
| `... check.ps1 balance [-N 200 -Seed 20261106]` | десятки минут | quick + симуляция `balance.ps1 check` + шлюз против эталона |
| `... check.ps1 full` | очень долго | quick + все стадии `tools\run_checks.ps1` |
| `... check.ps1 baseline` | мгновенно | сохранить последний прогон баланса как `balance/baseline.json` (коммитится) |
| `python tools/balance_gate.py diff [--run DIR]` | мгновенно | сравнить прогон с эталоном: победы (доверительные интервалы), время прохождения, новые замечания |
| `python tools/balance_gate.py list` | мгновенно | прогоны в `balance/out/` |

Код возврата 0 — всё хорошо, 1 — ошибки/регрессии. Шлюз сравнивает только сопоставимые прогоны (один seed и `--n`); иначе пишет предупреждение. Подробнее о симуляторе: [BALANCE_SIMULATOR.md](BALANCE_SIMULATOR.md).

# Lanternreach — playtest protocol (needs real players)

Turn on Settings → "Diagnostics (local only)" (or `LAMP_DIAG=1`). Data: `diagnostics.json` + one line per minute in `diagnostics_log.jsonl`.
After a session: `python tools/summarize_diag.py` prints the state at 15 min / 1 h / 3 h / 5 h / 10 h of play.

| Test | Player | What to watch / ask | Good result (metric) |
|---|---|---|---|
| First 15 min (3–5 new players, no help) | never saw the game | Where do they stop or look lost? Do they find the first lantern alone? "What would you do next?" at minute 15 | first_lantern < 8 min; first_spend < 4 min; ≥ 1 lantern lit and a furnace built; deaths ≤ 2; player names a next target (new lantern/chest/villager) |
| First hour | same players | New ways to gather/build appear and are noticed (pickaxe tier, forge, refinery, hub, first boss) | by 60 min: ≥ 6 lanterns, ≥ 4 building types, first guardian fought; mine_secs < 40% of play time |
| 3 hours | 2–3 players continuing | Goals other than collecting: puzzles, bosses, relic/synergy builds, exploration | ≥ 2 biomes opened; beacon ≥ 3; deaths/hour not rising; players give non-resource goals unprompted |
| 5–10 hours | 1–2 dedicated players | Did automation replace manual mining? Do biomes and bosses still differ? Is endgame (rifts, legends, decor) used? | mine_secs share falls hour over hour; fast_travel used; finale reached; rifts ≥ 1 clear; no long gaps with no new unlock |

Questions after each session: where were you bored? what did you not understand? which item/relic never mattered? did anything feel unfair (deaths, boss hits)?
Compare answers with the metrics; change the game only where both agree.

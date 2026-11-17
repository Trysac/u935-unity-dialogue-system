# Project record

## Objective

Run branching game dialogues from JSON graphs with typed variables, a bounded condition language, assignment nodes and localized lines with locale fallback

## Scope

- In scope: engine-independent dialogue runtime (parsing, validation, execution, state, localization).
- Out of scope: Unity editor tooling, visual graph editors, audio/animation integration, rich text support, pluralization rules, and persistent storage of `RunnerState` to disk.

## Timeline

| Date | Event |
|---|---|
| 2026 | Repository created from the factory utility template. |

## Effort metrics

| Metric | Value |
|---|---|
| Tokens (input / output) | 688196 / 34035 |
| Hours (human) | pending measurement |
| Cost (USD) | 0.7382 |

## Factory run u935 (measured from API usage)

| Date (UTC) | Phase | Model | Batch | Input | Output | Cache write | Cache read | Cost (USD) |
|---|---|---|---|---|---|---|---|---|
| 2026-11-17 | generation | google/gemini-4-argon | yes | 5966 | 26145 | 0 | 0 | 0.5468 |
| 2026-11-17 | fix-1 | google/gemini-3.8-flash | no | 47498 | 5542 | 0 | 89232 | 0.0631 |
| 2026-11-17 | audit | google/gemini-3.8-flash | no | 115879 | 945 | 0 | 409182 | 0.1211 |
| 2026-11-17 | docs | google/gemini-3.1-flash-lite | no | 20439 | 1403 | 0 | 0 | 0.0072 |

Total: $0.7382. Prices: vertex line, as measured by its adapter. Providers serve the current version of each model id, so the date tells which version answered; versions, launch stages and price sources per line are in the factory's model register (docs/MODELS.md).

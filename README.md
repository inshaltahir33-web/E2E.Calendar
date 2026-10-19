# E2E Calendar (Windows) — Project Pack

Native Windows 10/11 calendar + reminders + tasks app. Publisher: E2E Dev.
Stack: .NET 8, WinUI 3 (Windows App SDK), SQLite(SQLCipher), MSIX.

## Is pack me kya hai
| File | Kaam |
|---|---|
| AGENTS.md | OpenCode/agents ke liye rules (repo root me rakho) |
| opencode.json | OpenCode config |
| docs/01-ONE-DAY-PLAN.md | Ek din ka hour-by-hour plan |
| docs/02-ARCHITECTURE.md | Layers, DB schema, algorithms, security (A-Z) |
| docs/03-UI-UX.md | Layout wireframes, design tokens, screens |
| docs/04-FEATURES-HOWTO.md | Har feature kaise banega + done criteria |
| docs/05-GIT-WORKFLOW.md | Git/GitHub commands, branches, CI |
| prompts/P00..P12 | OpenCode ko order me dene wale prompts |

## Quick start
1. Windows PC pe install: VS 2022 (Workload: ".NET desktop" + "Windows App SDK C# templates"), .NET 8 SDK, Git, Node (OpenCode ke liye), VS Code.
2. `npm i -g opencode-ai` phir repo folder me `opencode`.
3. Is pack ko repo root me copy karo, `git init`, phir prompts P00 se shuru karo.

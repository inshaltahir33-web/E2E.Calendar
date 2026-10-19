# Git Workflow

## Pehli baar
```bash
git init -b main
git add -A && git commit -m "chore: initial project pack"
# GitHub pe empty private repo e2e-calendar banao, phir:
git remote add origin https://github.com/<username>/e2e-calendar.git
git push -u origin main
```
GitHub CLI ho to: `gh repo create e2e-calendar --private --source . --push`

## Branching
- `main` hamesha buildable. Feature branches: `feat/recurrence`, `feat/reminders`, `fix/...`
- Merge via Pull Request (ya `git merge --no-ff`).
- Tags: `v0.1.0` (day 1), `v0.2.0` (sync)...

## Commit style
`feat(reminders): add wake-from-sleep catch-up` · `fix(recurrence): handle DST gap` · `test: ...` · `docs: ...`

## CI (.github/workflows/build.yml)
```yaml
name: build
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '8.0.x' }
      - run: dotnet restore
      - run: dotnet build -c Release -p:Platform=x64 --no-restore
      - run: dotnet test tests/E2E.Calendar.Core.Tests -c Release
```
## Secrets
OAuth client IDs `dotnet user-secrets` ya GitHub Actions secrets me. Kabhi commit nahi.

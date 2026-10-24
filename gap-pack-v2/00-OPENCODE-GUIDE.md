# OpenCode ko prompts kaise dene hain (Windows + VS Code)

## 1. Zip ko repo me rakho
Zip ko apne calendar project ke **root** me extract karo. Root wo folder hai jisme `AGENTS.md` aur `docs/` already hain.
PowerShell (project root me):

    Expand-Archive -Path .\gap-pack-v2.zip -DestinationPath . -Force

Isse ye files aayengi: `docs/06-SYNC.md`, `docs/12-GAPS-ANALYSIS.md`, `docs/spec-original.txt`, `prompts/P13...P42`, `prompts/README-P13-P42.md`, aur ye guide.
Agar tumhare paas pehle se `docs/12-...` ya `docs/06-...` koi aur file hai to extract se pehle naam check kar lo (`-Force` overwrite kar deta hai).
Phir ek commit karo: `git add . ; git commit -m "docs: gap pack"` taa ke OpenCode galti kare to wapas ja sako.

## 2. Pehla message (sirf ek baar, naye session me)
OpenCode terminal me `opencode` chalao (VS Code ke terminal me project root se). Ye paste karo:

    Read AGENTS.md, docs/spec-original.txt, docs/12-GAPS-ANALYSIS.md and docs/06-SYNC.md.
    Do not write code yet. Reply in 10 lines max: what you understood about the app,
    the current state of the repo (which P/U prompts look done), and any conflict between
    the docs and the code. Then wait.

Ye check hai ke OpenCode ne sab samjha. Agar wo repo ki halat galat bataye to pehle usay sahi karo.

## 3. Har prompt ka loop (yehi baar baar karna hai)
1. `/new` se **naya session** (har prompt ke liye fresh context; purani baaton se confusion kam hota hai).
2. Ye template paste karo (sirf file name badlo):

       Read AGENTS.md, then read and execute prompts/P13-google-sync.md fully.
       Follow docs/06-SYNC.md and the UI rules in docs/08 and docs/11.
       Rules: do only this prompt, nothing else. Build, run the tests, fix failures.
       If something needs a secret/account/key from me, stop and tell me exactly what.
       At the end give: (1) files changed, (2) tests: pass/fail counts, (3) anything you skipped or faked.
       Then commit with the message written in the prompt.

3. Result padho. Agar "skipped/faked" me kuch aaya to usi session me bolo: `Finish what you skipped, no mocks`.
4. Khud `dotnet build` aur `dotnet test` chala ke dekho, app ek baar chala ke dekho. OpenCode ki "done" par bharosa mat karo, ek minute ka check karo.
5. Sab theek ho to agla prompt. Kuch toot gaya to `git log` se pichhla commit dekho, `/undo` ya `git reset --hard HEAD~1` se wapas aao.

Lamba prompt (P13, P14, P19, P20, P37) ek hi session me poora na ho to usay bolo: `Do steps 1-2 only, commit, stop.` phir naye session me `Continue P13 from step 3`.

## 4. Order (isi tarteeb se)
| Step | Prompts | Kab |
|---|---|---|
| 0 | U11, U12 (theme + UI bugs) | Sabse pehle, warna naye features bhi bigre UI par banenge |
| 1 | P13, P14, P15 | Sync block (Google, Microsoft, invites, birthdays) |
| 2 | P16, P17, P18 | Search/tags, holidays/regional, command palette/free slot |
| 3 | P19, P40 | Billing, paywall, premium themes aur fonts |
| 4 | P20, P21, P22 | Windows integration, event extras, print/PDF |
| 5 | P39, P41 | Analytics opt-out, mockups |
| 6 | P23, P25, P24, P42 | Reliability/perf/a11y, localisation, packaging, acceptance run. **MVP yahan Store ke liye tayyar** |
| 7 | P26-P33, P36 | v1.1 |
| 8 | P34, P35, P37, P38 | v2 |

Ek waqt me ek hi prompt. Parallel sessions ek hi repo me mat chalao.

## 5. Ye kaam OpenCode nahi kar sakta, tumhein khud karna hai
| Prompt | Tumhara kaam |
|---|---|
| P13 | Google Cloud Console me project + OAuth client (Desktop app) + Calendar API enable. Client ID config me daalo. Public users ke liye Google verification (privacy policy URL, demo video) |
| P14 | Azure portal me app registration (public client, redirect `http://localhost`), Graph permissions Calendars.ReadWrite, Contacts.Read, offline_access |
| P15 | Google People API enable karo, Graph contacts permission |
| P19 | Microsoft Partner Center account, Store me app reserve karna, subscriptions + lifetime add-on bana kar IDs code me daalna |
| P24 | Partner Center se publisher identity, signing certificate, WACK (Windows par chalta hai), screenshots, privacy policy ka public URL |
| P31 | iCloud ka app-specific password test ke liye |
| P36, P37 | Server hosting (Azure/Fly.io), domain, WNS credentials |
| P42 | Real accounts ke saath manual test scripts chalana |

Jahan secret chahiye wahan OpenCode ko kehna: key `dotnet user-secrets` ya environment variable se le, repo me kabhi commit na kare.

## 6. Jab OpenCode atak jaye
- Zyada lamba session ho gaya aur wo bhatakne laga: `/compact` ya `/new` aur prompt dobara do.
- UI wala kaam galat: screenshot paste karo aur bolo `docs/11 ke tokens use karo, hard-coded colour nahi`.
- Test fail par wo test hata raha ho: bolo `Do not delete or weaken tests. Fix the code.`
- Ye pack Claude ne spec se banaya hai. Agar koi prompt tumhare asli code structure (folder/class names) se mel na khaye to OpenCode ko bolo `Adapt names to the real code, keep the behaviour`.

[Release gate] Spec section 10 "MVP acceptance criteria". Run after P13-P25 + P39 are done.
Create docs/14-ACCEPTANCE.md with the 9 spec checkboxes as a table: criterion | how verified | automated test name or manual steps | result | date | evidence link.
1. Automate what can be automated: view perf with 10k events, quick add 20-phrase table, sync tests with fakes, reminder timing harness from P23, gating matrix, WACK result file.
2. For manual items (real Google + Microsoft accounts, sleep/wake/reboot, Store purchase in sandbox, Narrator/keyboard/high-contrast) write exact step-by-step scripts.
3. Run everything that can run here, fill the table honestly: PASS / FAIL / NOT RUN (never guess PASS). List every FAIL as a numbered fix task at the bottom.
4. Do not change code in this prompt except test additions; fixes are separate commits.
Commit "docs: mvp acceptance run".

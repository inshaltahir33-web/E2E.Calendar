[v2] Spec 6.1 "Urdu (Roman) and Arabic phrasing". Prereq: P08 (parser), P25 (localisation).
1. Refactor the NLP parser into ILanguagePack modules (English stays default and must keep passing its 20 test phrases unchanged). Auto-detect per input, user can force a language in Settings > General.
2. Roman Urdu pack: kal (tomorrow), aaj, parson, agle haftay/agla somwar, subah/dopahar/shaam/raat, "sham 5 baje", "dopahar 2 baje", "har jumma" (every Friday), "1 ghanta"/"aadha ghanta" durations, "yaad dilana"/"remind 30 min pehle", "#work", "!high". Numerals in Urdu script too.
3. Arabic pack (Modern Standard + common Gulf phrasing): اليوم، غداً، بعد غد، الجمعة القادمة، كل يوم أحد، الساعة 5 مساءً، قبل 30 دقيقة، تذكير. Arabic-Indic digits. RTL-safe live preview (FlowDirection per text run).
4. Ambiguity rules: week start from settings, "baje" without am/pm -> nearest sensible time and show it in the live preview so the user can fix it.
5. Add a data file per language (JSON of keywords) so more languages can be added without code.
Tests: >= 25 phrases per language as a table-driven test, mixed-language input, regression for English. Commit "feat: urdu and arabic quick add".

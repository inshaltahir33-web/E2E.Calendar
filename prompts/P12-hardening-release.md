Hardening pass against docs/02-ARCHITECTURE.md §8:
- Verify no secrets/tokens in DB or logs; Serilog filters; "Delete all data" wipes DB + credentials; ICS export in Privacy settings.
- Run `dotnet list package --vulnerable`. Add global exception handler.
- Check AutomationProperties, keyboard navigation, high-contrast, text scaling 200%.
- Update README (build/run instructions, architecture link), add LICENSE placeholder and CHANGELOG.md.
- Ensure `dotnet build` and `dotnet test` pass; list remaining known gaps in docs/07-KNOWN-GAPS.md.
Then print the git commands to tag v0.1.0 and push. Commit "chore: hardening and release v0.1.0".

Read AGENTS.md and docs/02-ARCHITECTURE.md fully.
Task: scaffold the solution.
1. Create E2E.Calendar.sln with projects exactly as in the "Solution layout": Core, Data, Sync, Platform (net8.0-windows10.0.19041.0), App (WinUI 3 packaged, Windows App SDK, x64+ARM64), and test projects Core.Tests, Data.Tests (xUnit, FluentAssertions, NSubstitute).
2. Add project references pointing inward only (App->Core/Data/Sync/Platform; Data/Sync/Platform->Core).
3. Add allowed NuGet packages only. Pin versions.
4. In App set up Generic Host + DI (App.xaml.cs), Serilog file logging, a MainWindow with Mica backdrop and a placeholder NavigationView.
5. Add .github/workflows/build.yml from docs/05-GIT-WORKFLOW.md.
6. Run dotnet build and dotnet test. Fix errors. Do not add features yet.
Commit: "chore: scaffold solution".

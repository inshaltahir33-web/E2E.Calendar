# E2E Calendar (Windows)

A native Windows 10/11 calendar, reminders, and task management application built with modern Microsoft technologies.

## Tech Stack
* **Framework:** WinUI 3 (Windows App SDK) / .NET 8
* **Language:** C#
* **Database:** SQLite (SQLCipher)
* **Packaging:** msix


## Project Structure

| File / Folder | Description |
| :--- | :--- |
| `AGENTS.md` | OpenCode development rules and agent guidelines |
| `opencode.json` | OpenCode configuration file |
| `docs/` | Comprehensive architecture documentation, UI/UX wireframes, and feature specifications |
| `prompts/` | Structured prompt sequences and historical development logs |
| `src/` | Main application source code |
| `tests/` | Unit and performance test suites |

## Quick Start

1. **Prerequisites:** Ensure you have Windows 10/11, .NET 8 SDK, and Visual Studio 2022 (with the *Desktop development with .NET* and *Windows App SDK C# templates* workloads) installed.
2. **Clone & Open:** Clone the repository and open `E2E.Calendar.sln` in Visual Studio or VS Code.
3. **Build & Run:** Run the application via Visual Studio or use the .NET CLI:
   ```bash
   dotnet build E2E.Calendar.sln -c Debug -p:Platform=x64
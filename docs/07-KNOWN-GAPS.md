# Known gaps and decisions

- The requested `docs/11` file is not present in this repository. The current
  sync source of truth is [docs/06-SYNC.md](./06-SYNC.md).
- `docs/13-WIRING-AUDIT.md` did not exist before this task; it is created by
  the wiring audit work.
- The existing settings schema has one `general.show_week_numbers` value. The
  typed settings contract exposes both week and month properties as the same
  persisted value until the schema is explicitly expanded.
- The shell state is the navigation source of truth, while the existing
  `NavigationService` remains the WinUI view materializer. This avoids changing
  view construction or layout in this no-visual-change task.
- Native WinUI screenshots at 1280x800 in Light and Dark themes could not be
  captured here because the available browser capture tools do not attach to
  desktop WinUI windows. Build and XAML compiler validation remain green.

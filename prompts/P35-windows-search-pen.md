[v2] Spec 7. Prereq: P16 (search).
1. Windows Search integration: expose events/reminders/tasks to Start search with a Windows Search property handler / the packaged-app ContentIndexer API (Windows.Storage.Search ContentIndexer, AppContentIndexer). Index title, time, location only (NOT notes). Update the index on every repo write; full re-index command in Settings > Privacy & data. Respect delete-all-data.
2. Result click launches the app by protocol (myapp://event/<id>) and opens that event.
3. Pen input: on Day/Week grid, InkCanvas overlay mode (toggle button + auto when a pen is detected). User writes a text on a time slot -> run Windows ink handwriting recognition (InkRecognizerContainer) -> quick-add parser (P08) with the slot as default time -> live preview -> Save. Fallback to typed text if recognition is empty.
4. Touch: minimum 40 px hit targets in grid and toasts, pinch/scroll panning check.
Tests: index entry mapping, protocol activation routing, recognition-to-parser unit test with fake recognizer. Commit "feat: windows search and pen input".

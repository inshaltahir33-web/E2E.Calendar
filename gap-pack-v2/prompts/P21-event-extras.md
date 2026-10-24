1. Rich notes: RichEditBox with bold/italic/list/link; store as sanitized Markdown (no scripts/HTML), render read-only in chips tooltip; sync maps to plain text for providers that need it.
2. Travel time: optional "Travel time" minutes in editor; renders a hatched block before the event; reminder planner can alert at (start - travel - offset).
3. Custom sounds: per-alert and per-calendar sound (bundled set + file picker for .wav/.mp3, validate length < 30s), Windows default option; used by toast (ms-appx/ms-appdata audio) and alarm popup. Bundle royalty-free sounds only and list licenses in About.
Tests for planner with travel offset. Commit "feat: rich notes, travel time, custom sounds".

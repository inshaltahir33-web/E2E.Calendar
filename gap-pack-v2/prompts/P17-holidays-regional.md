1. Holidays: bundle .ics files for PK, US, UK, AE, IN under Assets/Holidays (use only freely licensed data and put attribution in About). Settings > Calendars > Holidays: toggle per country, creates read-only calendars (own colour, e.g. Slate/Teal). Loaded lazily; not on startup path.
2. Regional defaults: first-run week start, 12/24h, date format from Windows (CultureInfo / Windows.Globalization.Calendar) with explicit override stored in settings; apply instantly everywhere via a single IRegionalSettings service.
3. Daily digest: add settings (enable, time) and make P07 digest use it.
4. Hooks for alternate calendars (Hijri etc.): IAlternateCalendarProvider interface + settings entry disabled/"coming soon" is NOT allowed in UI—just the interface, no UI.
Tests for week-start computations. Commit "feat: holidays and regional settings".

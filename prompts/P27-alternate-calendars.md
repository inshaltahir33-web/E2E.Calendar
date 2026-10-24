[v1.1] Spec 2.5. Prereq: P17 (regional settings).
1. IAlternateCalendarService with providers: Hijri (UmAlQuraCalendar + HijriCalendar with +/-2 day manual adjustment because moon sighting differs by country, important for Pakistan), Chinese lunar (ChineseLunisolarCalendar), Hebrew (HebrewCalendar). Windows.Globalization.Calendar for formatting.
2. Settings > General > "Secondary calendar": None / Hijri / Chinese / Hebrew + Hijri day adjustment (-2..+2).
3. Show the alternate date small and muted beside the Gregorian date: in Month cells (bottom-right), Day/Week header, mini-calendar tooltip, Agenda day header. Month name of the alternate calendar shown in the toolbar subtitle.
4. Optional bundled "Islamic occasions" holiday .ics (Ramadan start, Eid etc.) as a toggle in Holiday calendars - label them "dates may vary by 1 day".
5. Dates are DISPLAY ONLY. Never store alternate dates in DB and never touch RRULE.
6. Colour is not the only signal; text size must follow 200% scaling.
Tests: known conversions (e.g. 1 Ramadan 1447 range, Hebrew new year), adjustment shifts, no DB schema change. Commit "feat: alternate calendar display".

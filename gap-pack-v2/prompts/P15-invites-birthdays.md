1. Attendees: add attendees field to the event editor (chips with email, status icon) and model; send invites through the connected account on save (Google attendees / Graph attendees). Local calendars store attendees but cannot send.
2. Invitations: when a pulled event has self attendee status needsAction, show an "Invitation" InfoBar/row in event pane and a toast with Accept / Tentative / Decline; call provider RSVP API; optimistic update + outbox retry.
3. Birthdays: read-only import from Google People / Graph contacts (permission asked only when user enables "Show birthdays" in Settings > Calendars), create a read-only "Birthdays" calendar with yearly all-day events + default alert (day before 9:00 and on day).
Tests with fakes. Commit "feat: attendees, rsvp, birthdays".

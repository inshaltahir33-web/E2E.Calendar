[MVP-adjacent] Spec 9.3 "analytics anonymous and opt-out". Do BEFORE P24 (Store listing must match).
Keep privacy-first: tiny, honest, easy to switch off.
1. IAnalytics interface with a NullAnalytics default and ONE sink (Azure Application Insights OR a tiny self-hosted endpoint - make it config-swappable). Allowed events only (allow-list in code, anything else is dropped): app_start, view_opened(view), feature_used(name), paywall_shown, purchase_started/completed(plan), sync_error(provider, error_code), reminder_late(bucket). 
2. NEVER collect: titles, notes, locations, attendees, emails, account ids, file names, IP-derived location. Random install id (GUID, reset button), app version, OS version, locale only.
3. Settings > Privacy & data: toggle "Share anonymous usage data" (default per spec = ON with a clear first-run line in onboarding screen 3; if the owner prefers privacy-first, a single constant DefaultOptIn=false flips it). Link to "What is collected" page listing every event.
4. Respect toggle instantly (flush queue and clear on off). Offline queue max 200 events. Delete-all-data also resets the install id.
5. Update the privacy policy text file docs/privacy-policy.md to match exactly.
Tests: allow-list enforcement, nothing sent when off, payload snapshot contains no free text. Commit "feat: anonymous opt-out analytics".

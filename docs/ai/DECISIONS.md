# Decisions

## D001: Native Android presentation with independent planner core

- **Status:** Accepted implementation choice under the native MAUI Android request.
- **Decision:** Use C# MAUI controls and a plain .NET core library; no Blazor, WebView, web project, or server.
- **Rationale:** Native forms and Android lifecycle behavior meet the requested platform; an independent core permits fast recurrence/storage tests without a device.
- **Consequences:** UI behavior still needs Android checks; the core tests cannot establish layout or navigation correctness.

## D002: Local snapshot storage and derived recurring occurrences

- **Status:** Accepted initial implementation choice.
- **Decision:** Store versioned JSON atomically in app-private storage. Derive occurrences from recurrence rules and store completion by routine ID/date rather than pre-generating future tasks.
- **Rationale:** A personal offline planner needs little data and no database/service dependencies. Derived occurrences avoid background scheduling and duplicate tasks after restarts.
- **Consequences:** No cross-device sync. Editing rules changes what appears on past dates too; completion history is retained for matching dates. Short months skip missing dates. Uninstall may remove data. The `.bak` is the previous successful save, not an external backup service.

## D003: Native daily notification with permission-aware midnight scheduling

- **Status:** Accepted under the daily/sticky notification request.
- **Decision:** Use one Android notification with a silent channel, a delete receiver for sticky restoration, and a one-shot local-midnight wakeup alarm. Keep preferences/dismissal metadata separate from planner data; read the same persisted task queries used by Home and widgets.
- **Rationale:** Native notifications retain accessible system expansion and lock-screen behavior while the app supplies its palette/icon. A single alarm avoids a continuously running service and adapts to local time-zone/DST changes. SCHEDULE_EXACT_ALARM lets the user authorize precise delivery without claiming unrestricted alarm privileges.
- **Consequences:** Users must allow notifications and precise alarms for the full behavior. Android can hide or block notifications and stop the app; sticky mode recovers delivered delete events but cannot promise visibility against system controls. Ordinary dismissal persists only for its local date. Large summaries require opening Home to see all tasks.

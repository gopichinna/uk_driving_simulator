# First-Run & Settings Screens Specification

All player-facing copy lives in `content/countries/gb/strings/en/ui.json` (and `en-XA/ui.json`), written in plain B1 English (`<= 15` words per sentence, button labels `<= 40` characters, zero pressure language).

| Screen | `ui.json` Keys | Button / Action Behaviour |
|---|---|---|
| **1. Welcome** | `onboarding.welcome.title`, `onboarding.welcome.body`, `onboarding.welcome.btn_next` | `btn_next` ("Get started") advances to the 18+ age check. |
| **2. Age Check (18+)** | `onboarding.age.title`, `onboarding.age.body`, `onboarding.age.btn_confirm`, `onboarding.age.under_18_notice` | `btn_confirm` ("I am 18 or older") records confirmation and advances; under-18 users see `under_18_notice` and cannot proceed. |
| **3. Language Picker** | `onboarding.lang.title`, `onboarding.lang.body`, `onboarding.lang.option_en`, `onboarding.lang.btn_continue` | Stores preferred learning language locally for future updates (English only in v1); `btn_continue` ("Continue") advances to permissions. |
| **4. Permissions** | `onboarding.perms.title`, `onboarding.perms.audio_note`, `onboarding.perms.btn_continue`, `onboarding.perms.btn_skip` | Explains audio playback (and optional crash reports if enabled); no camera/location/contacts needed; `btn_continue` / `btn_skip` finishes onboarding. |
| **5. Free Tier Offer** | `offer.free.title`, `offer.free.body`, `offer.free.btn_start` | Explains that the first 3 lessons are free with no account needed; `btn_start` opens Stage 0 Lesson 1. |
| **6. Unlock Full Access (£14.99)** | `unlock.title`, `unlock.body`, `unlock.price_note`, `unlock.btn_buy`, `unlock.btn_restore`, `unlock.btn_later` | Honest one-off £14.99 unlock of all Great Britain lessons and mock tests (no subscription, no pressure language); `btn_buy` starts store purchase, `btn_restore` restores previous purchase, `btn_later` returns to free lessons. |
| **7. Restore Purchase** | `unlock.restore.checking`, `unlock.restore.success`, `unlock.restore.none`, `unlock.restore.error` | Displays clear status after querying Google Play / App Store billing. |
| **8. Settings** | `settings.title`, `settings.steering.label`, `settings.steering.hint`, `settings.aim_assist.label`, `settings.aim_assist.hint`, `settings.audio.voice_label`, `settings.audio.sfx_label`, `settings.language.label`, `settings.units.hint`, `settings.restore.btn`, `settings.privacy.btn`, `settings.reset_progress.label`, `settings.reset_progress.confirm`, `settings.close` | Adjusts steering sensitivity, aim assist, coach voice/SFX toggles, language preference, restores purchases, opens privacy policy, or resets local progress. |

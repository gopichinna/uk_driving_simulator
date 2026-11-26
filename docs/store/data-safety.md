# Google Play Data Safety Form — L to Local ("Keep Left")

Prepared from the current codebase (`unity/Assets/Scripts/` and `docs/04-architecture.md`) as of **2026-11-26**.
If Firebase, RevenueCat, or analytics are enabled in a future release, this form must be updated before shipping that build.

## Summary Answers (Play Console)
| Question | Answer | Evidence in Repo |
|---|---|---|
| Does your app collect or share any of the required user data types? | **No** | No network SDKs (`Firebase`, `Analytics`, `UnityWebRequest`, `HttpClient`, or ads) are present in `unity/Assets/Scripts/`. |
| Is all user data processed locally on the device? | **Yes** | Settings and lesson/review progress are stored locally via Unity `PlayerPrefs` (`TouchControls.cs`, `DriveGame.Audio.cs`, `LessonSession.cs`). |
| Does the app require an account or login? | **No** | Offline-first; no authentication flow exists. |
| Can the user delete their local data? | **Yes** | `DriveGame.Debrief.cs` calls `PlayerPrefs.DeleteAll()`, or the user can clear app storage in Android Settings / uninstall the app. |
| Does the app contain ads? | **No** | No ad SDKs are integrated. |

## What Is Stored Locally (`PlayerPrefs`)
- `steering_sensitivity` (`TouchControls.cs`)
- Sound and voice toggles (`DriveGame.Audio.cs`)
- Lesson completion progress, review schedule, and practice licence status (`LessonSession.cs`)

None of these values leave the device.

## Needs the owner
- Confirm whether the **first Play Store release** ships offline-only (matching the answers above) or will add Firebase / RevenueCat before launch (which would require updating Data Safety to declare purchase history / crash logs / device IDs).

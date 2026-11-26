# Google Play Release Checklist — L to Local ("Keep Left")

## 1. Build & Technical Requirements (from `unity/ProjectSettings/ProjectSettings.asset`)
- **Scripting Backend / Architecture:** IL2CPP with **ARM64** (`AndroidTargetArchitectures: 2`) — meets Google Play 64-bit requirement.
- **Min SDK:** Android 8.0 (API 26, `AndroidMinSdkVersion: 26`).
- **Target API Level:** `AndroidTargetSdkVersion: 0` (Automatic — verify the build machine has **API 34 or API 35** installed to meet current Google Play target-API rules).
- **Package Format:** Build an **Android App Bundle (`.aab`)** for Play Console upload (not just `.apk`).
- **Versioning:** Currently `bundleVersion: 1.0`, `AndroidBundleVersionCode: 1` (increment `versionCode` on every upload).
- **Signing Key:** Configure a dedicated upload keystore and enrol in **Play App Signing** (never commit keystores to Git).

## 2. Store Listing & Visual Assets
- **Listing Copy (English):** [content/store/listing/en.md](../../content/store/listing/en.md) (app name ≤ 30 chars, short description ≤ 80 chars, full description ≤ 4,000 chars, ADI & DVSA disclaimers, OGL + ODbL credits).
- **App Icon Concepts (1024×1024 PNG):** Choose 1 of 3 in `content/store/icon-concepts/` (note: Play Console requires **512×512 PNG** for the store icon; downscale the chosen 1024 px concept).
- **Lesson Badges & Fault Icons:** `content/lessons/gb/badges/` (256×256 PNG + SVG) and `content/ui/icons/` (96×96 PNG + SVG).
- **Screenshots & Feature Graphic:** Capture phone screenshots (16:9 / 9:16, >= 2 required) and a 1024×500 feature graphic from the Unity build.

## 3. Compliance & Testing Track
- **Data Safety:** [data-safety.md](data-safety.md)
- **Content Rating (IARC):** [content-rating.md](content-rating.md)
- **Privacy Policy:** [privacy-policy.md](privacy-policy.md)
- **Testing Track:** Upload first `.aab` to **Internal Testing**, verify install + audio + all 7 courses on a physical Android device, then promote to **Closed Testing** (12 testers / 14 days if required for personal Play accounts) before Production.

## Needs the owner
- Choose 1 of the 3 app icon concepts in `content/store/icon-concepts/` and export/resize to `512x512` for Play Console.
- Create and back up the Android upload keystore (outside Git) and enrol in Play App Signing.
- Provide `<owner legal name / trading name>`, `<owner email>`, and a public HTTPS URL for `privacy-policy.md`.
- Confirm whether the first release is free/offline-only or includes the £14.99 RevenueCat unlock.
- Confirm the final Google Play target API level (API 34/35) installed on the Unity build machine.

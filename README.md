# Tuntum anaa Kɔkɔɔ

A card guessing game. The top card of a shuffled deck is face down: call **Tuntum** (black ♠♣) or **Kɔkɔɔ** (red ♥♦).
Right: +1 and go again. Wrong: your turn ends. When all 52 cards are gone, the most points wins.

Unity game for **web, Android and iOS**, in the "Pop Party" look: sunny yellow, thick black outlines, chunky shadows.

- **Play vs Computer**: the computer counts the colours already drawn and usually calls the colour with more cards left. It slips sometimes.
- **Pass & Play**: two players share one phone.
- **Play Nearby (no internet)**: up to 4 phones on one phone's hotspot. The host taps Host, friends join the hotspot and tap Join. Android and iPhone together.
- **2 to 4 players** in every mode except Play Online (2).
- **Play Online**: one player creates a room and gets a 4-letter code; a friend joins with it. The server shuffles and decides every guess, so nobody can cheat.
- Card animations: deal from the deck, lift-and-flip, fly into the score box (right) or shake and spin onto the misses pile (wrong).
- Sound effects, all synthesised in code: deal swish, flip snap, a chime that climbs with the streak, buzzer, whoosh, fanfares.
- **3 in a row**: a crowd cheers, the player's avatar jumps and spins, suit symbols burst out, a "3 IN A ROW!" ribbon pops up. Bigger cheers and banners at 5, 7, 10 (ON FIRE!, UNSTOPPABLE!, LEGENDARY!).

## Get the game (no setup needed)

Every push to `main` runs **Build apps** (`.github/workflows/app.yml`), which packages the game in `App/www` for all three platforms:

- **Web**: `Tuntum-Web` artifact, and published to GitHub Pages when Pages is set to *GitHub Actions* in Settings → Pages.
- **Android**: `Tuntum-Android` artifact holds `Tuntum.apk`; copy it to an Android phone and open it to install.
- **iOS**: `Tuntum-iOS` artifact holds an unsigned `.ipa`, a Simulator build and the Xcode project. Putting it on a real iPhone needs signing with an Apple Developer account ($99/year).

Find them under **Actions → Build apps → the latest run → Artifacts**.

The Unity project below is the long-term version and builds once a Unity license is added.

## Project layout

| Folder | What it is |
|---|---|
| `Assets/Scripts/Core` | The rules (deck, match, streaks, computer player). Plain C#, no Unity. |
| `Assets/Scripts/Game` | Screens, animations, sound synth, celebrations. All UI is built in code. |
| `Assets/Scripts/Net` | WebSocket client for online play ([NativeWebSocket](https://github.com/endel/NativeWebSocket)). |
| `Assets/Scripts/Editor/ProjectSetup.cs` | App name, bundle id `com.johnlartey.tuntum`, portrait, store settings. |
| `Assets/Tests/EditMode` | Rule tests. |
| `App/` | The playable game (HTML/JS) and its Android/iOS packaging (Capacitor). |
| `Server/` | Node.js room server for online play, with tests, Dockerfile and Fly.io config. |
| `.github/workflows` | Cloud builds and server deploy. |

Unity version: **2022.3.45f1 LTS** (any 2022.3 works). Open the folder in Unity Hub and press Play; the game builds itself at start-up from `Assets/Scenes/Main.unity`.

## Cloud builds (web, Android, iOS)

Every push to `main` runs `.github/workflows/build.yml`. Results appear under the repository's **Actions** tab → the run → **Artifacts**:

- `Tuntum-Web`: the web game (also published to GitHub Pages if Pages is on)
- `Tuntum-Android APK`: install directly on an Android phone to test
- `Tuntum-Android App Bundle`: the `.aab` to upload to Google Play
- `Tuntum-iOS Xcode project`: open in Xcode on a Mac, or let the iOS job turn it into a TestFlight build

### 1. Unity license (required for any game build)

Add these under **Settings → Secrets and variables → Actions → New repository secret**:

| Secret | Value |
|---|---|
| `UNITY_EMAIL` | your Unity account email |
| `UNITY_PASSWORD` | your Unity account password |
| `UNITY_LICENSE` | the full text of your `Unity_lic.ulf` file (Unity Personal is free) |

To get `Unity_lic.ulf`: install Unity Hub, sign in, *Preferences → Licenses → Add → Get a free personal license*. The file is in
`C:\ProgramData\Unity\Unity_lic.ulf` (Windows) or `/Library/Application Support/Unity/Unity_lic.ulf` (Mac).
Full guide: https://game.ci/docs/github/activation. A Unity Pro serial works too as `UNITY_SERIAL`.

Without these secrets only the server tests run.

### 2. Android signing (needed for Google Play)

The APK is signed with a debug key, which is fine for testing. For Google Play make an upload key once:

```
keytool -genkey -v -keystore tuntum.keystore -alias tuntum -keyalg RSA -keysize 2048 -validity 10000
base64 -w0 tuntum.keystore   # copy the output
```

Secrets: `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME` (`tuntum`), `ANDROID_KEYALIAS_PASS`.
Keep the keystore file safe: Google Play needs the same key for every update.

### 3. iOS (needs an Apple Developer account, $99/year)

Without Apple secrets you still get the Xcode project to build on a Mac. For automatic TestFlight builds, create in the
Apple Developer site an App ID `com.johnlartey.tuntum`, an *Apple Distribution* certificate and an *App Store* provisioning
profile, and an App Store Connect API key. Then add:

`APPLE_TEAM_ID`, `APPLE_CERTIFICATE_P12_BASE64`, `APPLE_CERTIFICATE_PASSWORD`, `APPLE_PROVISIONING_PROFILE_BASE64`,
`APP_STORE_CONNECT_KEY_ID`, `APP_STORE_CONNECT_ISSUER_ID`, `APP_STORE_CONNECT_KEY_P8_BASE64`.

### 4. Web game on GitHub Pages

*Settings → Pages → Build and deployment → Source: GitHub Actions*. (Pages on a private repository needs a paid GitHub plan; otherwise upload the `Tuntum-Web` artifact to itch.io.)

## Online server

```
cd Server
npm install
npm test        # 7 tests, including a full 52-card game between two simulated phones
npm start       # ws://localhost:8080
```

Hosting on Fly.io (about $0–2 a month at small scale; it sleeps when nobody plays):

1. Make a free account at https://fly.io and install `flyctl`.
2. `cd Server && fly launch --copy-config --no-deploy` (keep the name `tuntum-anaa-kokoo` or change it in `fly.toml`).
3. `fly tokens create deploy` and save it as the `FLY_API_TOKEN` secret. Pushes that change `Server/` then deploy it.

The game connects to `wss://tuntum-anaa-kokoo.fly.dev` (set in `Assets/Scripts/Net/OnlineClient.cs`). If you choose a different
app name, change `DefaultServer` there too.

## Fonts

Lilita One and Figtree (SIL Open Font License), with the Twi letters ɔ and Ɔ added from DejaVu Sans; DejaVu Sans for the card suits (Bitstream Vera license). See `Assets/Resources/Fonts/LICENSES.md`.

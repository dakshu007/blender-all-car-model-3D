# Tester builds: macOS .dmg and Windows .exe

Version and label: `UnityProject/Assets/BlossomRally/Scripts/BuildInfo.cs` (currently **0.11.0, "Test build"**,
shown on the title screen and in the corner while driving). Bump `Version` for each tester round.

## Option A: build both on your Mac (recommended, about 10 minutes)

1. **Unity Hub → Installs → your Unity → ⚙ → Add modules** and tick:
   - **Mac Build Support (Mono)** (usually already installed)
   - **Windows Build Support (Mono)** (this lets your Mac build the Windows .exe)

   Restart Unity after installing.
2. Open the project, then choose **Blossom Rally → Build Tester Release (Mac DMG + Windows EXE)**.
3. When it finishes, Finder opens `UnityProject/Builds/Release-0.11.0/` with:
   - `BlossomRally-0.11.0-mac.dmg`: a Universal app for Intel and Apple Silicon, with a drag-to-Applications window.
   - `BlossomRally-0.11.0-windows.zip`: contains `BlossomRally.exe` plus its data folder (portable, no install).

Single-platform builds are also in the menu: **Build macOS DMG**, **Build Windows EXE**.

## Option B: build in the cloud with GitHub Actions (adds a Windows Setup.exe installer)

The workflow is in `.github/workflows/build-game.yml`. Set it up once:

1. On GitHub, go to repo **Settings → Secrets and variables → Actions → New repository secret** and add:
   - `UNITY_EMAIL`: your Unity account email
   - `UNITY_PASSWORD`: your Unity password
   - `UNITY_LICENSE`: the whole text of `/Library/Application Support/Unity/Unity_lic.ulf` from your Mac
     (open it in TextEdit and copy everything). A Unity Personal licence works.
2. Go to **Actions → Build tester release → Run workflow**. After about 30–60 minutes the run's **Artifacts** contain:
   - `BlossomRally-mac-dmg`: `BlossomRally-0.11.0-mac.dmg`
   - `BlossomRally-windows`: `BlossomRally-Setup-0.11.0.exe` (installer with Start-menu and desktop shortcuts)
     and `BlossomRally-0.11.0-windows.zip`
3. To make a public download page, push a tag: `git tag v0.11.0 && git push origin v0.11.0`.
   This creates a **GitHub Release (pre-release)** with all three files attached. Link to those files from your landing page.

The cloud build uses the Unity version in `UnityProject/ProjectSettings/ProjectVersion.txt`.

## Branding for the landing page

The `Branding/` folder (at the repo root) has everything for the landing page and store pages:
- `AppIcon_1024_rounded.png` (app icon, rounded corners), `AppIcon_1024_square.png` (full-bleed square)
- `Logo_Banner_1920x1080.png` (hero image), `Logo_Social_1200x630.png` (link preview / social share image)
- `Logo_Wordmark.png` (transparent "BLOSSOM RALLY" title with the red seal), `BlossomRally.ico` (Windows icon)
- `render_car.py` + `compose.py` recreate them: a Blender render of the Brenner S1, then composed with Pillow

The same badge is the icon of the Mac app and the Windows exe, and the wordmark is on the title screen.

## Weekly updates

The title screen, the pause menu and the download notes say: **"New update every Thursday at 12 PM - fresh
improvements every week!"** Change the text in `Scripts/BuildInfo.cs` (`UpdateNote`), e.g. to add your time zone.

## What testers will see (unsigned test builds)

These builds aren't code-signed or notarized yet, so both systems warn the first time. Put this on the
landing page (the same text is inside the DMG and the zip):

- **macOS:** drag Blossom Rally to Applications, then **right-click → Open → Open**. On macOS 15 and later, if it's
  blocked, go to **System Settings → Privacy & Security → Open Anyway**.
- **Windows:** when SmartScreen shows "Windows protected your PC", click **More info → Run anyway**.

To remove these warnings for a public release, you need:
- an **Apple Developer ID** ($99/year) to sign and notarize the app;
- a **Windows code-signing certificate**.

Both can be added to the workflow later.

## Before sending a build

- Play it once on your own Mac from the .dmg: install, launch, drive a stage.
- If you can, check the Windows build on a real PC. It runs on Direct3D 11, a different graphics backend from your
  Mac's Metal, and the new shaders haven't been seen there yet.
- Ask testers for: their machine (CPU and GPU), the FPS shown by **F1**, screenshots (**O**), and how the driving felt.

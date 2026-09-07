# PassVault for Linux

A local, offline Linux password manager - the Linux counterpart to the PassVault Android and
Windows apps. Everything is stored only on this computer, encrypted, protected by a pattern
lock, with security questions as a recovery option if you ever forget your pattern or move to a
new computer.

## How to run it

A ready-to-run build is attached to the GitHub Release - no installation needed:

1. Download `PassVaultLinux` from the Release.
2. Make it runnable: `chmod +x PassVaultLinux`
3. Run it: `./PassVaultLinux`

If you'd rather build it yourself from source, install the [.NET 10 SDK](https://dotnet.microsoft.com/download)
and run `dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true`
from the `PassVaultLinux` folder.

## What it does

- Stores your passwords (title, username, password, URL, notes) in an encrypted vault, unlocked
  by drawing your pattern.
- Generates strong random passwords and shows a strength meter while you type your own.
- Copies a password to the clipboard and auto-clears it after 30 seconds.
- Keeps a separate encrypted photo vault for pictures you want stored the same secure way.
- Logs login activity, including failed attempts, and shows a banner if someone tried to get in.
- Lets you export/import an encrypted backup file (`.pvbk`) - the same format the Android and
  Windows apps use, so you can move your passwords between this computer and your phone or PC.
- Light/Dark/System theme toggle.
- No network access at all - nothing leaves this computer except a backup file you explicitly export.

## Security model

Mirrors the Android and Windows apps' design exactly, so a backup file interchanges cleanly
between all three:

- **Data Encryption Key (DEK)**: one random 256-bit key generated once, used to encrypt the
  vault (AES-256-GCM). Never derived from your pattern directly.
- **Two wrapped copies of the DEK**: one wrapped under a key derived from your pattern
  (PBKDF2-HMAC-SHA256, salted, 210,000 iterations), one wrapped under a key derived the same way
  from your security question answers. Either path unlocks the same vault.
- **At rest**: a single `vault.dat` file (and a `photos_index.dat` + one file per photo) under
  `~/.local/share/PassVault/`, AES-GCM encrypted. Metadata (salts, wrapped DEK copies, security
  question text, settings, login activity) lives in a file encrypted with a random key kept in
  its own owner-only-readable file (`machine.key`, `chmod 600`) - the Linux analogue of the
  Windows app's DPAPI-protected storage.
- **No network access at all** - the app makes no HTTP calls anywhere in the codebase, so
  nothing ever leaves this computer except via the manual, password-protected `.pvbk` export you
  trigger yourself.
- **Cross-platform backups**: the `.pvbk` file format and key-derivation parameters are
  byte-identical to the Android and Windows apps', so a backup exported from any of them imports
  cleanly on the others. For best compatibility, stick to plain ASCII characters (letters,
  numbers, common symbols) in your backup password and security answers.
- Copied passwords auto-clear from the clipboard after 30 seconds.

## Project layout

- `Crypto/` - AES-GCM helpers and PBKDF2 key derivation (byte-compatible with the other apps).
- `Auth/` - pattern and security-question managers; encrypted settings storage.
- `Data/` - the `Credential`/`VaultPhoto` models, the encrypted vault and photo stores, and `.pvbk` backup export/import.
- `Controls/` - the custom pattern-lock control, a password field with a show/hide toggle, and a Yes/Cancel confirmation dialog.
- `Views/` - one Avalonia UserControl per screen (onboarding, login, vault, settings, etc.).
- `AppState.cs` - single shared session/state holder (mirrors the other apps' equivalents).
- `MainWindow.axaml.cs` - screen navigation and the auto-lock logic.

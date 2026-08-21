# Publishing an update

ModelTimer checks its GitHub repo's **Releases** for new versions (via
[Velopack](https://velopack.io)). Every installed copy points at the same repo, so publishing a
release there is what makes "an update is available" show up on every machine running the app.

The repo (`acelandingpages-crypto/modeltimer`) is **public**. That's deliberate: it means the app
needs no embedded token at all to check for updates - there's no secret inside the shipped binary
for anyone to extract. (A private repo was tried first, but fine-grained GitHub PATs were
unreliable for this repo/account, and "public repo, no token" is strictly safer than "private repo,
token embedded in every install" anyway. No user or fan data is ever in this repo - only app
source and packaged releases.)

## One-time setup

You need exactly one token, used **only locally, only by you, only to publish** - it never ships
inside the app.

- Create a **fine-grained** token at https://github.com/settings/personal-access-tokens/new,
  scoped to **Only select repositories → modeltimer**, with Repository permissions →
  **Contents: Read and write**.
- If fine-grained tokens are still being flaky for this repo, a **classic** token
  (https://github.com/settings/tokens/new, `repo` scope) works too - it's broader (all your
  repos, not just this one), but since it's local-only and never distributed, that's a much
  smaller risk than embedding it in the app would have been.
- Keep it out of source control. Set it as an environment variable when you publish, don't paste
  it into a file.

## Publishing a new version

1. Bump `<Version>` in `ModelTimer.csproj` (Velopack uses this to detect updates - it must
   increase every release, e.g. `1.0.0` → `1.0.1`).

2. Build, pack, and upload:
   ```powershell
   $env:VELOPACK_PUBLISH_TOKEN = "<your publish token>"
   .\publish.ps1
   ```
   This publishes a self-contained build, packages it with `vpk`, and uploads it as a new
   GitHub Release on the repo.

3. That's it. Every installed copy checks for updates on startup (and via Settings → Check for
   Updates) and will offer to update to this version.

## First install

Velopack updates only work for copies *installed* through its installer - running the raw `.exe`
from a publish folder, or via `dotnet run`, has nothing to update itself into. The first release's
`Setup.exe` (produced by `vpk pack`, uploaded alongside the update packages) is what moderators
should actually install; after that, updates apply themselves in place.

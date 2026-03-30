# Add Scoop Bucket Support

## Context

gh-tray currently supports installation via winget and direct download. The user wants to add Scoop package manager support by hosting a Scoop bucket directly in this repo (no separate bucket repository).

## Approach

Scoop looks for manifests in a `bucket/` directory when a repo is added as a bucket. We need:

1. A manifest JSON file in `bucket/gh-tray.json`
2. A release workflow job to update the manifest's version and hash on each release
3. README update with install instructions

Users will install via:
```
scoop bucket add gh-tray https://github.com/jsfr/gh-tray
scoop install gh-tray
```

## Files to Create/Modify

### 1. Create `bucket/gh-tray.json`

Scoop manifest using the standalone exe (not the installer — Scoop prefers portable apps). The `#/gh-tray.exe` URL fragment tells Scoop to rename the download.

```json
{
    "version": "0.0.5",
    "description": "A Windows system tray application that monitors your GitHub pull requests using the gh CLI",
    "homepage": "https://github.com/jsfr/gh-tray",
    "license": "MIT",
    "url": "https://github.com/jsfr/gh-tray/releases/download/v0.0.5/gh-tray-win-x64.exe#/gh-tray.exe",
    "hash": "<backfill from current release>",
    "bin": "gh-tray.exe",
    "shortcuts": [
        ["gh-tray.exe", "gh-tray"]
    ],
    "checkver": "github",
    "autoupdate": {
        "url": "https://github.com/jsfr/gh-tray/releases/download/v$version/gh-tray-win-x64.exe#/gh-tray.exe",
        "hash": {
            "url": "https://github.com/jsfr/gh-tray/releases/download/v$version/gh-tray-win-x64.exe",
            "mode": "download"
        }
    }
}
```

- `license: "MIT"` — user confirmed MIT
- `checkver: "github"` — uses GitHub Releases API to detect latest version
- `autoupdate` — lets `scoop update` compute the hash by downloading the exe
- No `persist` — the app already manages its config at `%APPDATA%\gh-tray`

### 2. Modify `.github/workflows/release.yml`

Add a `scoop` job after `release` that:
1. Checks out `main` (which already has the changelog commit from `release`)
2. Downloads the exe from the GitHub Release via `gh release download`
3. Computes sha256 hash
4. Updates `bucket/gh-tray.json` with `jq`
5. Commits and pushes to `main`

```yaml
  scoop:
    needs: release
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v4
        with:
          ref: main

      - name: Determine version
        id: version
        run: echo "version=${GITHUB_REF#refs/tags/v}" >> "$GITHUB_OUTPUT"

      - name: Download release artifact
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          gh release download "${{ github.ref_name }}" \
            --pattern "gh-tray-win-x64.exe" \
            --dir .

      - name: Compute hash
        id: hash
        run: |
          hash=$(sha256sum gh-tray-win-x64.exe | awk '{print $1}')
          echo "sha256=$hash" >> "$GITHUB_OUTPUT"

      - name: Update Scoop manifest
        env:
          VERSION: ${{ steps.version.outputs.version }}
          HASH: ${{ steps.hash.outputs.sha256 }}
        run: |
          jq \
            --arg version "$VERSION" \
            --arg hash "$HASH" \
            '.version = $version | .hash = $hash | .url = "https://github.com/jsfr/gh-tray/releases/download/v" + $version + "/gh-tray-win-x64.exe#/gh-tray.exe"' \
            bucket/gh-tray.json > bucket/gh-tray.json.tmp
          mv bucket/gh-tray.json.tmp bucket/gh-tray.json

      - name: Commit manifest update
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add bucket/gh-tray.json
          git diff --cached --quiet || git commit -m "chore: update scoop manifest for ${{ github.ref_name }}"
          git push origin HEAD:main
```

### 3. Modify `README.md`

Add Scoop section after winget (around line 36), before `### Installer`:

```markdown
### Scoop

```
scoop bucket add gh-tray https://github.com/jsfr/gh-tray
scoop install gh-tray
```
```

## Decisions

- **License**: MIT (no LICENSE file added — just the manifest field)
- **Backfill hash**: No — leave placeholder, manifest becomes valid after next release
- **Shortcuts**: Yes — include Start Menu shortcut

## Verification

1. Validate manifest JSON: `python -m json.tool bucket/gh-tray.json`
2. After next release, verify the workflow job runs and commits the updated manifest
3. Test install: `scoop bucket add gh-tray https://github.com/jsfr/gh-tray && scoop install gh-tray`
4. Verify `gh-tray` is available on PATH after install

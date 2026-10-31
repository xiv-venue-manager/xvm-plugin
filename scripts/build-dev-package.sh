#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

STAGE=${1:?usage: build-dev-package.sh <stage-dir>}
BUCKET=${XVM_DEV_BUCKET:-plugin-dev}
BASE_URL=${XVM_DEV_BASE_URL:-https://media.xivvenuemanager.com/$BUCKET}

VERSION="$(( $(date -u +%Y) - 2026 )).$(( 10#$(date -u +%m%d) )).$(( 10#$(date -u +%H%M) ))"
OUT=VenueManager/bin/x64/Release-Dev
ZIP_NAME="XIVVenueManagerSyncDev-$VERSION.zip"

dotnet build VenueManager.sln -c Release --no-incremental -p:Flavour=Dev -p:Version="$VERSION"

mkdir -p "$STAGE"
cp "$OUT/XIVVenueManagerSyncDev/XIVVenueManagerSync.zip" "$STAGE/$ZIP_NAME"
cp images/icon.png "$STAGE/icon.png"

python3 - "$OUT/XIVVenueManagerSyncDev/XIVVenueManagerSyncDev.json" "$STAGE/repo.json" "$BASE_URL" "$ZIP_NAME" <<'PY'
import json, sys
manifest_path, out_path, base, zip_name = sys.argv[1:]
entry = json.load(open(manifest_path))
download = f"{base}/{zip_name}"
entry.update({
    "RepoUrl": "https://xivvenuemanager.com",
    "IconUrl": f"{base}/icon.png",
    "DownloadLinkInstall": download,
    "DownloadLinkUpdate": download,
    "IsHide": False,
})
json.dump([entry], open(out_path, "w"), indent=2)
PY

echo "Built $VERSION"

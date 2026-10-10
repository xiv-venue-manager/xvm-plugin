#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

SERVER=${XVM_DEV_SERVER:-server@192.168.1.122}
BUCKET=${XVM_DEV_BUCKET:-plugin-dev}
BASE_URL=${XVM_DEV_BASE_URL:-https://media.xivvenuemanager.com/$BUCKET}

VERSION="$(( $(date +%Y) - 2026 )).$(( 10#$(date +%m%d) )).$(( 10#$(date +%H%M) ))"
OUT=VenueManager/bin/x64/Release-Dev
ZIP_NAME="XIVVenueManagerSyncDev-$VERSION.zip"
STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT

dotnet build VenueManager.sln -c Release --no-incremental -p:Flavour=Dev -p:Version="$VERSION"

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

REMOTE_DIR=/tmp/xvm-plugin-dev-upload
ssh "$SERVER" "rm -rf $REMOTE_DIR && mkdir -p $REMOTE_DIR"
scp -q "$STAGE"/* "$SERVER:$REMOTE_DIR/"
ssh "$SERVER" "docker cp $REMOTE_DIR minio:/tmp/xvm-plugin-dev-upload && rm -rf $REMOTE_DIR && docker exec minio sh -c '
  mc alias set local http://localhost:9000 \"\$MINIO_ROOT_USER\" \"\$MINIO_ROOT_PASSWORD\" >/dev/null &&
  mc mb --ignore-existing local/$BUCKET &&
  mc anonymous set download local/$BUCKET &&
  mc cp /tmp/xvm-plugin-dev-upload/* local/$BUCKET/ &&
  rm -rf /tmp/xvm-plugin-dev-upload'"

echo "Published $VERSION"
echo "Custom repository URL: $BASE_URL/repo.json"

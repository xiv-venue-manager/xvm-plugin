#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

SERVER=${XVM_DEV_SERVER:-server@192.168.1.122}
BUCKET=${XVM_DEV_BUCKET:-plugin-dev}
BASE_URL=${XVM_DEV_BASE_URL:-https://media.xivvenuemanager.com/$BUCKET}

STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT

scripts/build-dev-package.sh "$STAGE"

REMOTE_DIR=/tmp/xvm-plugin-dev-upload
ssh "$SERVER" "rm -rf $REMOTE_DIR && mkdir -p $REMOTE_DIR"
scp -q "$STAGE"/* "$SERVER:$REMOTE_DIR/"
ssh "$SERVER" "docker cp $REMOTE_DIR minio:/tmp/xvm-plugin-dev-upload && rm -rf $REMOTE_DIR && docker exec minio sh -c '
  mc alias set local http://localhost:9000 \"\$MINIO_ROOT_USER\" \"\$MINIO_ROOT_PASSWORD\" >/dev/null &&
  mc mb --ignore-existing local/$BUCKET &&
  mc anonymous set download local/$BUCKET &&
  mc cp /tmp/xvm-plugin-dev-upload/* local/$BUCKET/ &&
  rm -rf /tmp/xvm-plugin-dev-upload'"

echo "Custom repository URL: $BASE_URL/repo.json"

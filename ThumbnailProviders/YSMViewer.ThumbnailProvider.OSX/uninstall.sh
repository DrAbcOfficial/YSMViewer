#!/usr/bin/env sh
set -eu

rm -rf "$HOME/Library/QuickLook/YSMViewerThumbnailProvider.qlgenerator"

qlmanage -r >/dev/null 2>&1 || true
qlmanage -r cache >/dev/null 2>&1 || true

echo "Uninstalled YSM Quick Look thumbnail provider."

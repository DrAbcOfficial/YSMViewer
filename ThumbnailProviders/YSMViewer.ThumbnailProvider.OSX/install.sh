#!/usr/bin/env sh
set -eu

src_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
bundle="$src_dir/YSMViewerThumbnailProvider.qlgenerator"
target_dir="$HOME/Library/QuickLook"

if [ ! -d "$bundle" ]; then
  echo "YSMViewerThumbnailProvider.qlgenerator not found next to install.sh" >&2
  exit 1
fi

mkdir -p "$target_dir"
rm -rf "$target_dir/YSMViewerThumbnailProvider.qlgenerator"
cp -R "$bundle" "$target_dir/"

qlmanage -r >/dev/null 2>&1 || true
qlmanage -r cache >/dev/null 2>&1 || true

echo "Installed YSM Quick Look thumbnail provider."

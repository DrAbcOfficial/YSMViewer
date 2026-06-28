#!/usr/bin/env sh
set -eu

rm -f "$HOME/.local/bin/ysm-thumbnailer"
rm -f "$HOME/.local/bin/libYSMViewer.ThumbnailProvider.so"
rm -f "$HOME/.local/bin/YSMViewer.ThumbnailProvider.so"
rm -f "$HOME/.local/share/thumbnailers/ysm.thumbnailer"
rm -f "$HOME/.local/share/mime/packages/application-vnd-ysm-model-encrypted.xml"

if command -v update-mime-database >/dev/null 2>&1; then
  update-mime-database "$HOME/.local/share/mime"
fi

echo "Uninstalled YSM XDG thumbnailer."

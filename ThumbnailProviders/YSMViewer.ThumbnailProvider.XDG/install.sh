#!/usr/bin/env sh
set -eu

src_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
bin_dir="$HOME/.local/bin"
thumb_dir="$HOME/.local/share/thumbnailers"
mime_dir="$HOME/.local/share/mime/packages"
exec_path="$bin_dir/ysm-thumbnailer"

mkdir -p "$bin_dir" "$thumb_dir" "$mime_dir"

cp "$src_dir/ysm-thumbnailer" "$exec_path"
chmod +x "$exec_path"

if [ -f "$src_dir/libYSMViewer.ThumbnailProvider.so" ]; then
  cp "$src_dir/libYSMViewer.ThumbnailProvider.so" "$bin_dir/libYSMViewer.ThumbnailProvider.so"
elif [ -f "$src_dir/YSMViewer.ThumbnailProvider.so" ]; then
  cp "$src_dir/YSMViewer.ThumbnailProvider.so" "$bin_dir/YSMViewer.ThumbnailProvider.so"
else
  echo "warning: YSMViewer.ThumbnailProvider native library not found next to install.sh" >&2
fi

sed "s|@EXEC_PATH@|$exec_path|g" "$src_dir/ysm.thumbnailer.in" > "$thumb_dir/ysm.thumbnailer"
cp "$src_dir/application-vnd-ysm-model-encrypted.xml" "$mime_dir/application-vnd-ysm-model-encrypted.xml"

if command -v update-mime-database >/dev/null 2>&1; then
  update-mime-database "$HOME/.local/share/mime"
else
  echo "warning: update-mime-database not found; MIME cache was not refreshed" >&2
fi

echo "Installed YSM XDG thumbnailer. Restart your file manager if thumbnails do not appear."

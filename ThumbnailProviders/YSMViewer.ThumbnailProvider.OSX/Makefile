BUNDLE_NAME := YSMViewerThumbnailProvider.qlgenerator
EXECUTABLE := YSMViewerThumbnailProvider
BUILD_DIR ?= build
NATIVE_LIB ?=

.PHONY: all clean

all: $(BUILD_DIR)/$(BUNDLE_NAME)/Contents/MacOS/$(EXECUTABLE)

$(BUILD_DIR)/$(BUNDLE_NAME)/Contents/MacOS/$(EXECUTABLE): GenerateThumbnailForURL.m Info.plist
	mkdir -p "$(BUILD_DIR)/$(BUNDLE_NAME)/Contents/MacOS"
	cp Info.plist "$(BUILD_DIR)/$(BUNDLE_NAME)/Contents/Info.plist"
	clang -bundle -fobjc-arc -framework CoreFoundation -framework CoreGraphics -framework QuickLook -o "$@" GenerateThumbnailForURL.m
	if [ -n "$(NATIVE_LIB)" ]; then cp "$(NATIVE_LIB)" "$(BUILD_DIR)/$(BUNDLE_NAME)/Contents/MacOS/libYSMViewer.ThumbnailProvider.dylib"; fi

clean:
	rm -rf "$(BUILD_DIR)"

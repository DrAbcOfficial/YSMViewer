CC ?= cc
CFLAGS ?= -O2 -Wall -Wextra -std=c11
LDFLAGS ?= -ldl
BUILD_DIR ?= build
TARGET := ysm-thumbnailer

.PHONY: all clean

all: $(BUILD_DIR)/$(TARGET)

$(BUILD_DIR)/$(TARGET): ysm-thumbnailer.c
	mkdir -p "$(BUILD_DIR)"
	$(CC) $(CFLAGS) -o "$@" ysm-thumbnailer.c $(LDFLAGS)

clean:
	rm -rf "$(BUILD_DIR)"

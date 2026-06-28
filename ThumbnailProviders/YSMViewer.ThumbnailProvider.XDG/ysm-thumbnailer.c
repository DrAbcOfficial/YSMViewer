#define _POSIX_C_SOURCE 200809L

#include <dlfcn.h>
#include <errno.h>
#include <limits.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

typedef void* (*YsmCreateFn)(const uint8_t* data, int length);
typedef int (*YsmRenderFn)(void* ctx, uint8_t* bgra, int width, int height);
typedef void (*YsmDestroyFn)(void* ctx);

typedef struct YsmApi {
    void* library;
    YsmCreateFn create;
    YsmRenderFn render;
    YsmDestroyFn destroy;
} YsmApi;

static uint32_t crc32_table[256];
static int crc32_ready = 0;

static void init_crc32(void)
{
    if (crc32_ready)
        return;

    for (uint32_t i = 0; i < 256; i++) {
        uint32_t c = i;
        for (int j = 0; j < 8; j++)
            c = (c & 1) ? 0xedb88320u ^ (c >> 1) : c >> 1;
        crc32_table[i] = c;
    }
    crc32_ready = 1;
}

static uint32_t crc32_update(uint32_t crc, const uint8_t* data, size_t len)
{
    init_crc32();
    crc = ~crc;
    for (size_t i = 0; i < len; i++)
        crc = crc32_table[(crc ^ data[i]) & 0xff] ^ (crc >> 8);
    return ~crc;
}

static uint32_t adler32_update(uint32_t adler, const uint8_t* data, size_t len)
{
    uint32_t a = adler & 0xffff;
    uint32_t b = (adler >> 16) & 0xffff;
    for (size_t i = 0; i < len; i++) {
        a = (a + data[i]) % 65521u;
        b = (b + a) % 65521u;
    }
    return (b << 16) | a;
}

static int write_be32(FILE* file, uint32_t value)
{
    uint8_t bytes[4] = {
        (uint8_t)(value >> 24),
        (uint8_t)(value >> 16),
        (uint8_t)(value >> 8),
        (uint8_t)value
    };
    return fwrite(bytes, 1, sizeof(bytes), file) == sizeof(bytes) ? 0 : -1;
}

static int write_chunk(FILE* file, const char type[4], const uint8_t* data, uint32_t len)
{
    if (write_be32(file, len) != 0)
        return -1;
    if (fwrite(type, 1, 4, file) != 4)
        return -1;
    if (len > 0 && fwrite(data, 1, len, file) != len)
        return -1;

    uint32_t crc = crc32_update(0, (const uint8_t*)type, 4);
    crc = crc32_update(crc, data, len);
    return write_be32(file, crc);
}

static int write_png(const char* path, const uint8_t* bgra, int width, int height)
{
    size_t stride = (size_t)width * 4 + 1;
    size_t raw_len = stride * (size_t)height;
    uint8_t* raw = (uint8_t*)malloc(raw_len);
    if (!raw)
        return -1;

    for (int y = 0; y < height; y++) {
        uint8_t* row = raw + (size_t)y * stride;
        row[0] = 0;
        for (int x = 0; x < width; x++) {
            const uint8_t* src = bgra + ((size_t)y * width + x) * 4;
            uint8_t* dst = row + 1 + (size_t)x * 4;
            dst[0] = src[2];
            dst[1] = src[1];
            dst[2] = src[0];
            dst[3] = src[3];
        }
    }

    size_t blocks = (raw_len + 65534u) / 65535u;
    size_t zlib_len = 2 + raw_len + blocks * 5 + 4;
    uint8_t* zlib = (uint8_t*)malloc(zlib_len);
    if (!zlib) {
        free(raw);
        return -1;
    }

    size_t pos = 0;
    zlib[pos++] = 0x78;
    zlib[pos++] = 0x01;
    uint32_t adler = 1;
    size_t offset = 0;
    while (offset < raw_len) {
        uint16_t block_len = (uint16_t)((raw_len - offset) > 65535u ? 65535u : (raw_len - offset));
        int final = (offset + block_len == raw_len);
        zlib[pos++] = final ? 1 : 0;
        zlib[pos++] = (uint8_t)block_len;
        zlib[pos++] = (uint8_t)(block_len >> 8);
        uint16_t nlen = (uint16_t)~block_len;
        zlib[pos++] = (uint8_t)nlen;
        zlib[pos++] = (uint8_t)(nlen >> 8);
        memcpy(zlib + pos, raw + offset, block_len);
        adler = adler32_update(adler, raw + offset, block_len);
        pos += block_len;
        offset += block_len;
    }
    zlib[pos++] = (uint8_t)(adler >> 24);
    zlib[pos++] = (uint8_t)(adler >> 16);
    zlib[pos++] = (uint8_t)(adler >> 8);
    zlib[pos++] = (uint8_t)adler;

    FILE* file = fopen(path, "wb");
    if (!file) {
        free(zlib);
        free(raw);
        return -1;
    }

    static const uint8_t signature[8] = { 137, 80, 78, 71, 13, 10, 26, 10 };
    uint8_t ihdr[13] = {
        (uint8_t)(width >> 24), (uint8_t)(width >> 16), (uint8_t)(width >> 8), (uint8_t)width,
        (uint8_t)(height >> 24), (uint8_t)(height >> 16), (uint8_t)(height >> 8), (uint8_t)height,
        8, 6, 0, 0, 0
    };

    int ok = fwrite(signature, 1, sizeof(signature), file) == sizeof(signature)
        && write_chunk(file, "IHDR", ihdr, sizeof(ihdr)) == 0
        && write_chunk(file, "IDAT", zlib, (uint32_t)pos) == 0
        && write_chunk(file, "IEND", NULL, 0) == 0;

    fclose(file);
    free(zlib);
    free(raw);
    return ok ? 0 : -1;
}

static uint8_t* read_file(const char* path, size_t* length)
{
    FILE* file = fopen(path, "rb");
    if (!file)
        return NULL;

    if (fseek(file, 0, SEEK_END) != 0) {
        fclose(file);
        return NULL;
    }
    long size = ftell(file);
    if (size <= 0) {
        fclose(file);
        return NULL;
    }
    rewind(file);

    uint8_t* data = (uint8_t*)malloc((size_t)size);
    if (!data) {
        fclose(file);
        return NULL;
    }

    if (fread(data, 1, (size_t)size, file) != (size_t)size) {
        free(data);
        fclose(file);
        return NULL;
    }

    fclose(file);
    *length = (size_t)size;
    return data;
}

static int load_api(YsmApi* api)
{
    const char* configured = getenv("YSM_THUMBNAIL_PROVIDER_LIB");
    const char* candidates[8];
    int candidate_count = 0;

    if (configured && configured[0] != '\0')
        candidates[candidate_count++] = configured;

    candidates[candidate_count++] = "./libYSMViewer.ThumbnailProvider.so";
    candidates[candidate_count++] = "./YSMViewer.ThumbnailProvider.so";

    char exe_relative[4096];
    char exe_relative_alt[4096];
    ssize_t exe_len = readlink("/proc/self/exe", exe_relative, sizeof(exe_relative) - 1);
    if (exe_len > 0) {
        exe_relative[exe_len] = '\0';
        char* slash = strrchr(exe_relative, '/');
        if (slash) {
            slash[1] = '\0';
            strncpy(exe_relative_alt, exe_relative, sizeof(exe_relative_alt) - 1);
            exe_relative_alt[sizeof(exe_relative_alt) - 1] = '\0';
            strncat(exe_relative, "libYSMViewer.ThumbnailProvider.so", sizeof(exe_relative) - strlen(exe_relative) - 1);
            strncat(exe_relative_alt, "YSMViewer.ThumbnailProvider.so", sizeof(exe_relative_alt) - strlen(exe_relative_alt) - 1);
            candidates[candidate_count++] = exe_relative;
            candidates[candidate_count++] = exe_relative_alt;
        }
    }

    candidates[candidate_count++] = "libYSMViewer.ThumbnailProvider.so";
    candidates[candidate_count++] = "YSMViewer.ThumbnailProvider.so";
    candidates[candidate_count] = NULL;

    for (int i = 0; candidates[i] != NULL; i++) {
        api->library = dlopen(candidates[i], RTLD_NOW | RTLD_LOCAL);
        if (api->library)
            break;
    }

    if (!api->library)
        return -1;

    api->create = (YsmCreateFn)dlsym(api->library, "YsmThumbnail_Create");
    api->render = (YsmRenderFn)dlsym(api->library, "YsmThumbnail_Render");
    api->destroy = (YsmDestroyFn)dlsym(api->library, "YsmThumbnail_Destroy");
    if (!api->create || !api->render || !api->destroy)
        return -1;

    return 0;
}

static int parse_size(const char* value)
{
    if (!value)
        return 256;
    char* end = NULL;
    long size = strtol(value, &end, 10);
    if (end == value || size < 1)
        return 256;
    if (size > 1024)
        return 1024;
    return (int)size;
}

int main(int argc, char** argv)
{
    if (argc < 3 || argc > 4) {
        fprintf(stderr, "Usage: ysm-thumbnailer INPUT OUTPUT [SIZE]\n");
        return 2;
    }

    int size = parse_size(argc == 4 ? argv[3] : NULL);
    size_t data_len = 0;
    uint8_t* data = read_file(argv[1], &data_len);
    if (!data || data_len > INT32_MAX) {
        fprintf(stderr, "Failed to read input file: %s\n", argv[1]);
        free(data);
        return 1;
    }

    YsmApi api = { 0 };
    if (load_api(&api) != 0) {
        fprintf(stderr, "Failed to load libYSMViewer.ThumbnailProvider.so\n");
        free(data);
        if (api.library)
            dlclose(api.library);
        return 1;
    }

    void* ctx = api.create(data, (int)data_len);
    free(data);
    if (!ctx) {
        dlclose(api.library);
        return 1;
    }

    uint8_t* bgra = (uint8_t*)calloc((size_t)size * (size_t)size * 4, 1);
    if (!bgra) {
        api.destroy(ctx);
        dlclose(api.library);
        return 1;
    }

    int result = api.render(ctx, bgra, size, size);
    if (result == 0)
        result = write_png(argv[2], bgra, size, size);

    free(bgra);
    api.destroy(ctx);
    dlclose(api.library);
    return result == 0 ? 0 : 1;
}

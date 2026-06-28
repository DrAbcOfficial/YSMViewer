#include <CoreFoundation/CoreFoundation.h>
#include <CoreGraphics/CoreGraphics.h>
#include <QuickLook/QuickLook.h>
#include <dlfcn.h>
#include <stdint.h>
#include <stdlib.h>

typedef void* (*YsmCreateFn)(const uint8_t* data, int length);
typedef int (*YsmRenderFn)(void* ctx, uint8_t* bgra, int width, int height);
typedef void (*YsmDestroyFn)(void* ctx);

static void* LoadNativeLibrary(void)
{
    Dl_info info;
    if (dladdr((const void*)&LoadNativeLibrary, &info) == 0 || info.dli_fname == NULL)
        return NULL;

    CFStringRef executablePath = CFStringCreateWithCString(kCFAllocatorDefault, info.dli_fname, kCFStringEncodingUTF8);
    if (executablePath == NULL)
        return NULL;

    CFURLRef executableUrl = CFURLCreateWithFileSystemPath(kCFAllocatorDefault, executablePath, kCFURLPOSIXPathStyle, false);
    CFRelease(executablePath);
    if (executableUrl == NULL)
        return NULL;

    CFURLRef dirUrl = CFURLCreateCopyDeletingLastPathComponent(kCFAllocatorDefault, executableUrl);
    CFRelease(executableUrl);
    if (dirUrl == NULL)
        return NULL;

    CFStringRef libraryNames[] = {
        CFSTR("libYSMViewer.ThumbnailProvider.dylib"),
        CFSTR("YSMViewer.ThumbnailProvider.dylib")
    };

    void* library = NULL;
    for (int i = 0; i < 2 && library == NULL; i++)
    {
        CFURLRef libraryUrl = CFURLCreateCopyAppendingPathComponent(kCFAllocatorDefault, dirUrl, libraryNames[i], false);
        if (libraryUrl == NULL)
            continue;

        char libraryPath[4096];
        Boolean ok = CFURLGetFileSystemRepresentation(libraryUrl, true, (UInt8*)libraryPath, sizeof(libraryPath));
        CFRelease(libraryUrl);
        if (ok)
            library = dlopen(libraryPath, RTLD_NOW | RTLD_LOCAL);
    }

    CFRelease(dirUrl);
    return library;
}

OSStatus GenerateThumbnailForURL(
    void* thisInterface,
    QLThumbnailRequestRef thumbnail,
    CFURLRef url,
    CFStringRef contentTypeUTI,
    CFDictionaryRef options,
    CGSize maxSize)
{
    (void)thisInterface;
    (void)contentTypeUTI;
    (void)options;

    CFDataRef fileData = NULL;
    SInt32 errorCode = 0;
    if (!CFURLCreateDataAndPropertiesFromResource(kCFAllocatorDefault, url, &fileData, NULL, NULL, &errorCode) || fileData == NULL)
        return noErr;

    void* library = LoadNativeLibrary();
    if (library == NULL)
    {
        CFRelease(fileData);
        return noErr;
    }

    YsmCreateFn ysmCreate = (YsmCreateFn)dlsym(library, "YsmThumbnail_Create");
    YsmRenderFn ysmRender = (YsmRenderFn)dlsym(library, "YsmThumbnail_Render");
    YsmDestroyFn ysmDestroy = (YsmDestroyFn)dlsym(library, "YsmThumbnail_Destroy");
    if (ysmCreate == NULL || ysmRender == NULL || ysmDestroy == NULL)
    {
        dlclose(library);
        CFRelease(fileData);
        return noErr;
    }

    CFIndex length = CFDataGetLength(fileData);
    if (length <= 0 || length > INT32_MAX)
    {
        dlclose(library);
        CFRelease(fileData);
        return noErr;
    }

    void* ctx = ysmCreate(CFDataGetBytePtr(fileData), (int)length);
    CFRelease(fileData);
    if (ctx == NULL)
    {
        dlclose(library);
        return noErr;
    }

    int size = (int)maxSize.width;
    if ((int)maxSize.height < size)
        size = (int)maxSize.height;
    if (size < 1)
        size = 256;
    if (size > 1024)
        size = 1024;

    size_t bufferSize = (size_t)size * (size_t)size * 4;
    uint8_t* bgra = (uint8_t*)calloc(bufferSize, 1);
    if (bgra != NULL && ysmRender(ctx, bgra, size, size) == 0)
    {
        CGColorSpaceRef colorSpace = CGColorSpaceCreateDeviceRGB();
        CGDataProviderRef provider = CGDataProviderCreateWithData(NULL, bgra, bufferSize, NULL);
        if (colorSpace != NULL && provider != NULL)
        {
            CGImageRef image = CGImageCreate(
                size,
                size,
                8,
                32,
                (size_t)size * 4,
                colorSpace,
                kCGBitmapByteOrder32Little | kCGImageAlphaPremultipliedFirst,
                provider,
                NULL,
                false,
                kCGRenderingIntentDefault);

            if (image != NULL)
            {
                CGRect rect = CGRectMake(0, 0, size, size);
                CGContextRef context = QLThumbnailRequestCreateContext(thumbnail, CGSizeMake(size, size), false, NULL);
                if (context != NULL)
                {
                    CGContextDrawImage(context, rect, image);
                    QLThumbnailRequestFlushContext(thumbnail, context);
                    CFRelease(context);
                }
                CGImageRelease(image);
            }
        }

        if (provider != NULL)
            CGDataProviderRelease(provider);
        if (colorSpace != NULL)
            CGColorSpaceRelease(colorSpace);
    }

    free(bgra);
    ysmDestroy(ctx);
    dlclose(library);
    return noErr;
}

void CancelThumbnailGeneration(void* thisInterface, QLThumbnailRequestRef thumbnail)
{
    (void)thisInterface;
    (void)thumbnail;
}

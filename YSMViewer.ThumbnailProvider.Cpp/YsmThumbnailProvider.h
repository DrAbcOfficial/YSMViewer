#pragma once
#include <Windows.h>
#include <thumbcache.h>
#include <stdint.h>

extern LONG g_lockCount;

// C# NativeAOT function signatures (context-based, BGRA output)
typedef void* (*YsmCreateFn)(const uint8_t* data, int length);
typedef int (*YsmRenderFn)(void* ctx, uint8_t* bgra, int width, int height);
typedef void (*YsmDestroyFn)(void* ctx);

class YsmThumbnailProvider : public IThumbnailProvider, public IInitializeWithStream
{
public:
    YsmThumbnailProvider();
    ~YsmThumbnailProvider();

    // IUnknown
    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    STDMETHODIMP_(ULONG) AddRef() override;
    STDMETHODIMP_(ULONG) Release() override;

    // IInitializeWithStream
    STDMETHODIMP Initialize(IStream* pStream, DWORD grfMode) override;

    // IThumbnailProvider
    STDMETHODIMP GetThumbnail(UINT cx, HBITMAP* phbmp, WTS_ALPHATYPE* pdwAlpha) override;

private:
    LONG m_refCount;
    uint8_t* m_fileData;
    size_t m_fileDataSize;

    HMODULE m_hYsmDll;
    YsmCreateFn m_ysmCreate;
    YsmRenderFn m_ysmRender;
    YsmDestroyFn m_ysmDestroy;
    void* m_ctx;
    bool m_initialized;

    bool LoadYsmDll();
    void UnloadYsmDll();
    static BOOL GetDllDir(WCHAR* buffer, size_t bufferSize);
};

class YsmClassFactory : public IClassFactory
{
public:
    YsmClassFactory();

    STDMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    STDMETHODIMP_(ULONG) AddRef() override;
    STDMETHODIMP_(ULONG) Release() override;
    STDMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv) override;
    STDMETHODIMP LockServer(BOOL fLock) override;

private:
    LONG m_refCount;
};

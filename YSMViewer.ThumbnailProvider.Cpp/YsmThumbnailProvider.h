#pragma once
#include <Windows.h>
#include <thumbcache.h>
#include <string>
#include <vector>

// C# NativeAOT function signatures
typedef int (*YsmInitFn)(const uint8_t* data, int length);
typedef int (*YsmRenderFn)(uint8_t* rgba, int width, int height);
typedef void (*YsmFreeFn)();

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
    std::vector<uint8_t> m_fileData;

    HMODULE m_hYsmDll;
    YsmInitFn m_ysmInit;
    YsmRenderFn m_ysmRender;
    YsmFreeFn m_ysmFree;
    bool m_initialized;

    bool LoadYsmDll();
    void UnloadYsmDll();
    static std::wstring GetDllDir();
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

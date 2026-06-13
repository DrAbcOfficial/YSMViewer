#include "YsmThumbnailProvider.h"
#include <string>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "gdi32.lib")
#pragma comment(lib, "ole32.lib")

//-----------------------------------------------------------------------------
// YsmThumbnailProvider
//-----------------------------------------------------------------------------

YsmThumbnailProvider::YsmThumbnailProvider()
    : m_refCount(1)
    , m_hYsmDll(nullptr)
    , m_ysmCreate(nullptr)
    , m_ysmRender(nullptr)
    , m_ysmDestroy(nullptr)
    , m_ctx(nullptr)
    , m_initialized(false)
{
    InterlockedIncrement(&g_lockCount);
}

YsmThumbnailProvider::~YsmThumbnailProvider()
{
    if (m_ctx && m_ysmDestroy)
        m_ysmDestroy(m_ctx);
    m_ctx = nullptr;
    m_initialized = false;
    UnloadYsmDll();
    InterlockedDecrement(&g_lockCount);
}

STDMETHODIMP YsmThumbnailProvider::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;

    if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IThumbnailProvider))
        *ppv = static_cast<IThumbnailProvider*>(this);
    else if (IsEqualIID(riid, IID_IInitializeWithStream))
        *ppv = static_cast<IInitializeWithStream*>(this);
    else
        return E_NOINTERFACE;

    AddRef();
    return S_OK;
}

STDMETHODIMP_(ULONG) YsmThumbnailProvider::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

STDMETHODIMP_(ULONG) YsmThumbnailProvider::Release()
{
    LONG ref = InterlockedDecrement(&m_refCount);
    if (ref == 0)
        delete this;
    return ref;
}

STDMETHODIMP YsmThumbnailProvider::Initialize(IStream* pStream, DWORD grfMode)
{
    if (!pStream)
        return E_INVALIDARG;

    // Read stream data
    STATSTG stat;
    HRESULT hr = pStream->Stat(&stat, STATFLAG_NONAME);
    if (FAILED(hr) && hr != E_NOTIMPL)
        return hr;

    // Try to get file size; if Stat fails, just read until empty
    ULONGLONG fileSize = 0;
    if (SUCCEEDED(hr))
        fileSize = stat.cbSize.QuadPart;

    m_fileData.clear();

    if (fileSize > 0)
    {
        m_fileData.resize(static_cast<size_t>(fileSize));
        ULONG bytesRead = 0;
        hr = pStream->Read(m_fileData.data(), static_cast<ULONG>(fileSize), &bytesRead);
        if (FAILED(hr))
            return hr;
        m_fileData.resize(bytesRead);
    }
    else
    {
        // Read in chunks
        uint8_t buffer[65536];
        while (true)
        {
            ULONG bytesRead = 0;
            hr = pStream->Read(buffer, sizeof(buffer), &bytesRead);
            if (FAILED(hr) || bytesRead == 0)
                break;
            m_fileData.insert(m_fileData.end(), buffer, buffer + bytesRead);
        }
    }

    // Load C# DLL and call Create
    if (!LoadYsmDll())
        return E_FAIL;

    m_ctx = m_ysmCreate(m_fileData.data(), static_cast<int>(m_fileData.size()));
    if (!m_ctx)
        return E_FAIL;

    m_initialized = true;
    return S_OK;
}

STDMETHODIMP YsmThumbnailProvider::GetThumbnail(UINT cx, HBITMAP* phbmp, WTS_ALPHATYPE* pdwAlpha)
{
    if (!phbmp || !pdwAlpha)
        return E_INVALIDARG;

    *phbmp = nullptr;
    *pdwAlpha = WTSAT_UNKNOWN;

    if (!m_initialized || !m_ysmRender || !m_ctx)
        return E_FAIL;

    int size = static_cast<int>(cx);
    if (size < 1) size = 1;
    if (size > 256) size = 256;

    // Allocate BGRA buffer — C# writes BGRA directly
    std::vector<uint8_t> bgra(size * size * 4);

    int result = m_ysmRender(m_ctx, bgra.data(), size, size);
    if (result != 0)
        return E_FAIL;

    // Create HBITMAP from BGRA buffer (Windows DIB is BGRA)
    BITMAPINFO bmi = {};
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = size;
    bmi.bmiHeader.biHeight = -size; // top-down
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    HDC hdcScreen = GetDC(nullptr);
    void* pBits = nullptr;
    HBITMAP hBitmap = CreateDIBSection(hdcScreen, &bmi, DIB_RGB_COLORS, &pBits, nullptr, 0);
    ReleaseDC(nullptr, hdcScreen);

    if (!hBitmap)
        return E_FAIL;

    // Direct copy — C# already outputs BGRA
    memcpy(pBits, bgra.data(), size * size * 4);

    *phbmp = hBitmap;
    *pdwAlpha = WTSAT_ARGB;
    return S_OK;
}

bool YsmThumbnailProvider::LoadYsmDll()
{
    if (m_hYsmDll)
        return true;

    std::wstring dllPath = GetDllDir() + L"\\YSMViewer.ThumbnailProvider.dll";
    m_hYsmDll = LoadLibraryW(dllPath.c_str());
    if (!m_hYsmDll)
        return false;

    m_ysmCreate = reinterpret_cast<YsmCreateFn>(GetProcAddress(m_hYsmDll, "YsmThumbnail_Create"));
    m_ysmRender = reinterpret_cast<YsmRenderFn>(GetProcAddress(m_hYsmDll, "YsmThumbnail_Render"));
    m_ysmDestroy = reinterpret_cast<YsmDestroyFn>(GetProcAddress(m_hYsmDll, "YsmThumbnail_Destroy"));

    if (!m_ysmCreate || !m_ysmRender || !m_ysmDestroy)
    {
        UnloadYsmDll();
        return false;
    }

    return true;
}

void YsmThumbnailProvider::UnloadYsmDll()
{
    if (m_hYsmDll)
    {
        FreeLibrary(m_hYsmDll);
        m_hYsmDll = nullptr;
    }
    m_ysmCreate = nullptr;
    m_ysmRender = nullptr;
    m_ysmDestroy = nullptr;
}

std::wstring YsmThumbnailProvider::GetDllDir()
{
    WCHAR path[MAX_PATH];
    HMODULE hModule;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        reinterpret_cast<LPCWSTR>(&GetDllDir), &hModule);
    GetModuleFileNameW(hModule, path, MAX_PATH);
    std::wstring fullPath(path);
    size_t pos = fullPath.find_last_of(L"\\/");
    return fullPath.substr(0, pos);
}

//-----------------------------------------------------------------------------
// YsmClassFactory
//-----------------------------------------------------------------------------

YsmClassFactory::YsmClassFactory()
    : m_refCount(1)
{
}

STDMETHODIMP YsmClassFactory::QueryInterface(REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;

    if (IsEqualIID(riid, IID_IUnknown) || IsEqualIID(riid, IID_IClassFactory))
        *ppv = static_cast<IClassFactory*>(this);
    else
        return E_NOINTERFACE;

    AddRef();
    return S_OK;
}

STDMETHODIMP_(ULONG) YsmClassFactory::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

STDMETHODIMP_(ULONG) YsmClassFactory::Release()
{
    LONG ref = InterlockedDecrement(&m_refCount);
    if (ref == 0)
        delete this;
    return ref;
}

STDMETHODIMP YsmClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;

    if (pUnkOuter)
        return CLASS_E_NOAGGREGATION;

    auto* provider = new YsmThumbnailProvider();
    HRESULT hr = provider->QueryInterface(riid, ppv);
    provider->Release();
    return hr;
}

STDMETHODIMP YsmClassFactory::LockServer(BOOL fLock)
{
    if (fLock)
        InterlockedIncrement(&g_lockCount);
    else
        InterlockedDecrement(&g_lockCount);
    return S_OK;
}

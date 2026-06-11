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
    , m_ysmInit(nullptr)
    , m_ysmRender(nullptr)
    , m_ysmFree(nullptr)
    , m_initialized(false)
{
}

YsmThumbnailProvider::~YsmThumbnailProvider()
{
    if (m_initialized && m_ysmFree)
        m_ysmFree();
    m_initialized = false;
    UnloadYsmDll();
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

    // Load C# DLL and call Init
    if (!LoadYsmDll())
        return E_FAIL;

    int result = m_ysmInit(m_fileData.data(), static_cast<int>(m_fileData.size()));
    if (result != 0)
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

    if (!m_initialized || !m_ysmRender)
        return E_FAIL;

    int size = static_cast<int>(cx);
    if (size < 1) size = 1;
    if (size > 256) size = 256;

    // Allocate RGBA buffer
    std::vector<uint8_t> rgba(size * size * 4);

    int result = m_ysmRender(rgba.data(), size, size);
    if (result != 0)
        return E_FAIL;

    // Create HBITMAP from RGBA buffer
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

    // Copy RGBA -> BGRA (Windows DIB is BGRA)
    uint8_t* dst = static_cast<uint8_t*>(pBits);
    for (int i = 0; i < size * size; i++)
    {
        dst[i * 4] = rgba[i * 4 + 2];     // B
        dst[i * 4 + 1] = rgba[i * 4 + 1]; // G
        dst[i * 4 + 2] = rgba[i * 4];     // R
        dst[i * 4 + 3] = rgba[i * 4 + 3]; // A
    }

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

    m_ysmInit = reinterpret_cast<YsmInitFn>(GetProcAddress(m_hYsmDll, "YsmThumbnail_Init"));
    m_ysmRender = reinterpret_cast<YsmRenderFn>(GetProcAddress(m_hYsmDll, "YsmThumbnail_Render"));
    m_ysmFree = reinterpret_cast<YsmFreeFn>(GetProcAddress(m_hYsmDll, "YsmThumbnail_Free"));

    if (!m_ysmInit || !m_ysmRender || !m_ysmFree)
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
    m_ysmInit = nullptr;
    m_ysmRender = nullptr;
    m_ysmFree = nullptr;
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
    return S_OK;
}

#include "YsmThumbnailProvider.h"
#include <new>
#include <stdio.h>
#include <string.h>

#pragma comment(lib, "shlwapi.lib")
#pragma comment(lib, "gdi32.lib")
#pragma comment(lib, "ole32.lib")

//-----------------------------------------------------------------------------
// YsmThumbnailProvider
//-----------------------------------------------------------------------------

YsmThumbnailProvider::YsmThumbnailProvider()
    : m_refCount(1)
    , m_fileData(nullptr)
    , m_fileDataSize(0)
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
    if (m_fileData)
    {
        delete[] m_fileData;
        m_fileData = nullptr;
        m_fileDataSize = 0;
    }
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

    // Validate fileSize fits in size_t
    if (fileSize > SIZE_MAX)
        return E_OUTOFMEMORY;

    // Free existing data
    if (m_fileData)
    {
        delete[] m_fileData;
        m_fileData = nullptr;
        m_fileDataSize = 0;
    }

    if (fileSize > 0)
    {
        size_t allocSize = static_cast<size_t>(fileSize);
        m_fileData = new (std::nothrow) uint8_t[allocSize];
        if (!m_fileData)
            return E_OUTOFMEMORY;
        m_fileDataSize = allocSize;
        if (fileSize > ULONG_MAX)
            return E_OUTOFMEMORY;
        ULONG bytesRead = 0;
        hr = pStream->Read(m_fileData, static_cast<ULONG>(fileSize), &bytesRead);
        if (FAILED(hr))
            return hr;
        m_fileDataSize = bytesRead;
    }
    else
    {
        // Read in chunks
        uint8_t* buffer = new (std::nothrow) uint8_t[65536];
        if (!buffer)
            return E_OUTOFMEMORY;
        size_t totalSize = 0;
        uint8_t* temp = nullptr;
        while (true)
        {
            ULONG bytesRead = 0;
            hr = pStream->Read(buffer, 65536, &bytesRead);
            if (FAILED(hr) || bytesRead == 0)
                break;
            // Overflow check
            if (bytesRead > SIZE_MAX - totalSize)
            {
                delete[] temp;
                delete[] buffer;
                return E_OUTOFMEMORY;
            }
            uint8_t* newTemp = new (std::nothrow) uint8_t[totalSize + bytesRead];
            if (!newTemp)
            {
                delete[] temp;
                delete[] buffer;
                return E_OUTOFMEMORY;
            }
            if (temp)
            {
                memcpy(newTemp, temp, totalSize);
                delete[] temp;
            }
            memcpy(newTemp + totalSize, buffer, bytesRead);
            temp = newTemp;
            totalSize += bytesRead;
        }
        delete[] buffer;
        m_fileData = temp;
        m_fileDataSize = totalSize;
    }

    // Load C# DLL and call Create
    if (!LoadYsmDll())
        return E_FAIL;

    if (m_fileDataSize > INT_MAX)
        return E_OUTOFMEMORY;
    m_ctx = m_ysmCreate(m_fileData, static_cast<int>(m_fileDataSize));
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
    size_t bgraSize = static_cast<size_t>(size) * size * 4;
    uint8_t* bgra = new (std::nothrow) uint8_t[bgraSize];
    if (!bgra)
        return E_OUTOFMEMORY;

    int result = m_ysmRender(m_ctx, bgra, size, size);
    if (result != 0)
    {
        delete[] bgra;
        return E_FAIL;
    }

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
    {
        delete[] bgra;
        return E_FAIL;
    }

    // Direct copy — C# already outputs BGRA
    memcpy(pBits, bgra, bgraSize);
    delete[] bgra;

    *phbmp = hBitmap;
    *pdwAlpha = WTSAT_ARGB;
    return S_OK;
}

bool YsmThumbnailProvider::LoadYsmDll()
{
    if (m_hYsmDll)
        return true;

    WCHAR dllDir[MAX_PATH];
    if (!GetDllDir(dllDir, MAX_PATH))
        return false;

    // Ensure combined path with suffix fits in MAX_PATH (including null)
    static const size_t SUFFIX_CHARS = sizeof(L"\\YSMViewer.ThumbnailProvider.dll") / sizeof(WCHAR) - 1;
    if (wcslen(dllDir) > MAX_PATH - SUFFIX_CHARS - 1)
        return false;
    WCHAR dllPath[MAX_PATH];
    _snwprintf_s(dllPath, MAX_PATH, L"%s\\YSMViewer.ThumbnailProvider.dll", dllDir);
    m_hYsmDll = LoadLibraryW(dllPath);
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

BOOL YsmThumbnailProvider::GetDllDir(WCHAR* buffer, size_t bufferSize)
{
    if (!buffer || bufferSize == 0)
        return FALSE;

    WCHAR path[MAX_PATH];
    HMODULE hModule;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
        reinterpret_cast<LPCWSTR>(&GetDllDir), &hModule);
    GetModuleFileNameW(hModule, path, MAX_PATH);
    // Find last backslash or forward slash
    WCHAR* lastSlash = NULL;
    for (WCHAR* p = path; *p; ++p)
    {
        if (*p == L'\\' || *p == L'/')
            lastSlash = p;
    }
    if (!lastSlash)
        return FALSE;
    size_t dirLen = lastSlash - path;
    if (dirLen >= bufferSize)
        return FALSE;
    wcsncpy(buffer, path, dirLen);
    buffer[dirLen] = L'\0';
    return TRUE;
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

    auto* provider = new (std::nothrow) YsmThumbnailProvider();
    if (!provider)
        return E_OUTOFMEMORY;
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

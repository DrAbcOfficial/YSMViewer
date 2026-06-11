#include "YsmThumbnailProvider.h"
#include <string>

// {F4E2C1A8-7B3D-4E5F-9A1C-2D8E6F0B4A3C}
static const CLSID CLSID_YsmThumbnailProvider =
{ 0xF4E2C1A8, 0x7B3D, 0x4E5F, { 0x9A, 0x1C, 0x2D, 0x8E, 0x6F, 0x0B, 0x4A, 0x3C } };

// {E357FCCD-A995-4576-B01F-234630154E96}
static const GUID GUID_ThumbnailHandler =
{ 0xE357FCCD, 0xA995, 0x4576, { 0xB0, 0x1F, 0x23, 0x46, 0x30, 0x15, 0x4E, 0x96 } };

static const WCHAR* CLSID_STR = L"{F4E2C1A8-7B3D-4E5F-9A1C-2D8E6F0B4A3C}";
static const WCHAR* PROG_ID = L"YSMViewer.ThumbnailProvider";
static const WCHAR* HANDLER_GUID_STR = L"{E357FCCD-A995-4576-B01F-234630154E96}";

static HMODULE g_hModule = nullptr;
static LONG g_lockCount = 0;

static std::wstring GetModulePath()
{
    WCHAR path[MAX_PATH];
    GetModuleFileNameW(g_hModule, path, MAX_PATH);
    return path;
}

static HRESULT WriteRegistryValue(HKEY hKeyRoot, const WCHAR* subKey, const WCHAR* valueName, const WCHAR* data)
{
    HKEY hKey;
    LONG result = RegCreateKeyExW(hKeyRoot, subKey, 0, nullptr, REG_OPTION_NON_VOLATILE,
        KEY_WRITE, nullptr, &hKey, nullptr);
    if (result != ERROR_SUCCESS)
        return E_FAIL;

    result = RegSetValueExW(hKey, valueName, 0, REG_SZ,
        reinterpret_cast<const BYTE*>(data), static_cast<DWORD>((wcslen(data) + 1) * sizeof(WCHAR)));
    RegCloseKey(hKey);
    return (result == ERROR_SUCCESS) ? S_OK : E_FAIL;
}

static HRESULT WriteRegistryDword(HKEY hKeyRoot, const WCHAR* subKey, const WCHAR* valueName, DWORD data)
{
    HKEY hKey;
    LONG result = RegCreateKeyExW(hKeyRoot, subKey, 0, nullptr, REG_OPTION_NON_VOLATILE,
        KEY_WRITE, nullptr, &hKey, nullptr);
    if (result != ERROR_SUCCESS)
        return E_FAIL;

    result = RegSetValueExW(hKey, valueName, 0, REG_DWORD,
        reinterpret_cast<const BYTE*>(&data), sizeof(DWORD));
    RegCloseKey(hKey);
    return (result == ERROR_SUCCESS) ? S_OK : E_FAIL;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
    }
    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    if (!ppv) return E_POINTER;
    *ppv = nullptr;

    if (!IsEqualCLSID(rclsid, CLSID_YsmThumbnailProvider))
        return CLASS_E_CLASSNOTAVAILABLE;

    auto* factory = new YsmClassFactory();
    HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

STDAPI DllCanUnloadNow()
{
    return (g_lockCount == 0) ? S_OK : S_FALSE;
}

STDAPI DllRegisterServer()
{
    std::wstring dllPath = GetModulePath();

    // HKCR\CLSID\{...}
    std::wstring clsidKey = L"CLSID\\";
    clsidKey += CLSID_STR;
    WriteRegistryValue(HKEY_CLASSES_ROOT, clsidKey.c_str(), nullptr, PROG_ID);

    std::wstring serverKey = clsidKey + L"\\InprocServer32";
    WriteRegistryValue(HKEY_CLASSES_ROOT, serverKey.c_str(), nullptr, dllPath.c_str());
    WriteRegistryValue(HKEY_CLASSES_ROOT, serverKey.c_str(), L"ThreadingModel", L"Both");
    WriteRegistryDword(HKEY_CLASSES_ROOT, clsidKey.c_str(), L"DisableProcessIsolation", 1);

    // HKCR\<ProgID>
    std::wstring progIdKey = PROG_ID;
    WriteRegistryValue(HKEY_CLASSES_ROOT, progIdKey.c_str(), nullptr, PROG_ID);
    WriteRegistryValue(HKEY_CLASSES_ROOT, (progIdKey + L"\\CLSID").c_str(), nullptr, CLSID_STR);

    // HKCR\.ysm\ShellEx\{...}
    std::wstring handlerKey = L".ysm\\ShellEx\\";
    handlerKey += HANDLER_GUID_STR;
    WriteRegistryValue(HKEY_CLASSES_ROOT, handlerKey.c_str(), nullptr, CLSID_STR);

    // Approved list
    std::wstring approvedKey = L"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Shell Extensions\\Approved";
    WriteRegistryValue(HKEY_LOCAL_MACHINE, approvedKey.c_str(), CLSID_STR, L"YSMViewer Thumbnail Provider");

    return S_OK;
}

STDAPI DllUnregisterServer()
{
    // HKCR\CLSID\{...}
    std::wstring clsidKey = L"CLSID\\";
    clsidKey += CLSID_STR;
    RegDeleteTreeW(HKEY_CLASSES_ROOT, clsidKey.c_str());

    // HKCR\<ProgID>
    RegDeleteTreeW(HKEY_CLASSES_ROOT, PROG_ID);

    // HKCR\.ysm\ShellEx\{...}
    std::wstring handlerKey = L".ysm\\ShellEx\\";
    handlerKey += HANDLER_GUID_STR;
    RegDeleteTreeW(HKEY_CLASSES_ROOT, handlerKey.c_str());

    // Approved list
    std::wstring approvedKey = L"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Shell Extensions\\Approved";
    HKEY hKey;
    if (RegOpenKeyExW(HKEY_LOCAL_MACHINE, approvedKey.c_str(), 0, KEY_SET_VALUE, &hKey) == ERROR_SUCCESS)
    {
        RegDeleteValueW(hKey, CLSID_STR);
        RegCloseKey(hKey);
    }

    return S_OK;
}

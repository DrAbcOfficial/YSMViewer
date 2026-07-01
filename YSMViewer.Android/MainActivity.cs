using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Net;
using Android.OS;
using Android.Webkit;
using Android.Widget;
using Java.Interop;
using Uri = Android.Net.Uri;

namespace YSMViewer.Android;

[Activity(
    Label = "@string/app_name",
    MainLauncher = true,
    HardwareAccelerated = true,
    Theme = "@style/AppTheme",
    LaunchMode = LaunchMode.SingleTop,
    Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "file",
    DataHost = "*",
    DataPathPattern = ".*\\.ysm")]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "content",
    DataHost = "*",
    DataPathPattern = ".*\\.ysm")]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "content",
    DataMimeType = "application/vnd.ysm.model+encrypted")]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "file",
    DataMimeType = "application/vnd.ysm.model+encrypted")]
public sealed class MainActivity : Activity
{
    private const int OpenModelRequestCode = 1001;
    private const string AppOrigin = "https://ysmviewer.app";
    private WebView? _webView;
    private FileInfo? _selectedModelFile;
    private bool _pendingModelLoad;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        _webView = new WebView(this);
        SetContentView(_webView);

#if DEBUG
        WebView.SetWebContentsDebuggingEnabled(true);
#endif

        var settings = _webView.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        settings.AllowFileAccess = false;
        settings.AllowContentAccess = false;
        settings.MediaPlaybackRequiresUserGesture = false;

        _webView.SetWebViewClient(new YsmWebViewClient(this));
        _webView.SetWebChromeClient(new WebChromeClient());
        _webView.AddJavascriptInterface(new AndroidBridge(this), "YSMAndroid");
        _webView.LoadUrl($"{AppOrigin}/wwwroot/index.html");

        HandleIncomingFileIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        HandleIncomingFileIntent(intent);
    }

    public override void OnBackPressed()
    {
        if (_webView?.CanGoBack() == true)
        {
            _webView.GoBack();
            return;
        }

        base.OnBackPressed();
    }

    internal void OpenModelFilePicker()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraMimeTypes, new[]
        {
            "application/zip",
            "application/octet-stream",
            "application/vnd.ysm.model+encrypted"
        });

        StartActivityForResult(Intent.CreateChooser(intent, "Open YSM Model"), OpenModelRequestCode);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode != OpenModelRequestCode || resultCode != Result.Ok || data?.Data is not { } uri)
            return;

        try
        {
            OpenModelFromUri(uri);
        }
        catch (Exception ex)
        {
            Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
        }
    }

    internal void TryLoadSelectedFileInWebView()
    {
        if (!_pendingModelLoad || _webView is null)
            return;

        _pendingModelLoad = false;
        _webView.Post(() =>
        {
            _webView.EvaluateJavascript(
                "globalThis.ysmLoadAndroidFile && globalThis.ysmLoadAndroidFile('/android-file/current')",
                null);
        });
    }

    internal Stream? OpenAssetOrSelectedFile(Uri uri, out string mimeType)
    {
        var path = uri.Path ?? string.Empty;

        if (path == "/android-file/current")
        {
            mimeType = "application/octet-stream";
            return _selectedModelFile?.Exists == true ? _selectedModelFile.OpenRead() : null;
        }

        if (path.StartsWith("/wwwroot/", StringComparison.Ordinal))
        {
            var assetPath = path.TrimStart('/');
            mimeType = GetMimeType(assetPath);
            return Assets?.Open(assetPath);
        }

        mimeType = "text/plain";
        return null;
    }

    private FileInfo CopySelectedFileToCache(Uri uri)
    {
        var file = new FileInfo(Path.Combine(CacheDir!.AbsolutePath, "selected-model.ysm"));
        using var input = ContentResolver!.OpenInputStream(uri)
            ?? throw new InvalidOperationException("Unable to open selected file.");
        using var output = file.Open(FileMode.Create, FileAccess.Write, FileShare.Read);
        input.CopyTo(output);
        return file;
    }

    private void HandleIncomingFileIntent(Intent? intent)
    {
        if (intent?.Action != Intent.ActionView || intent.Data is not { } uri)
            return;

        try
        {
            OpenModelFromUri(uri);
        }
        catch (Exception ex)
        {
            Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void OpenModelFromUri(Uri uri)
    {
        _selectedModelFile = CopySelectedFileToCache(uri);
        _pendingModelLoad = true;
        TryLoadSelectedFileInWebView();
    }

    private static string GetMimeType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "text/javascript",
            ".mjs" => "text/javascript",
            ".json" => "application/json",
            ".wasm" => "application/wasm",
            ".dat" => "application/octet-stream",
            ".dll" => "application/octet-stream",
            ".pdb" => "application/octet-stream",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".ico" => "image/x-icon",
            ".ttf" => "font/ttf",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };
    }

    private sealed class AndroidBridge(MainActivity activity) : Java.Lang.Object
    {
        [JavascriptInterface]
        [Export("openModelFile")]
        public void OpenModelFile()
        {
            activity.RunOnUiThread(activity.OpenModelFilePicker);
        }
    }

    private sealed class YsmWebViewClient(MainActivity activity) : WebViewClient
    {
        public override WebResourceResponse? ShouldInterceptRequest(WebView? view, IWebResourceRequest? request)
        {
            if (request?.Url is not { } uri || uri.Host != "ysmviewer.app")
                return base.ShouldInterceptRequest(view, request);

            try
            {
                var stream = activity.OpenAssetOrSelectedFile(uri, out var mimeType);
                return stream is null
                    ? new WebResourceResponse("text/plain", "UTF-8", new MemoryStream())
                    : new WebResourceResponse(mimeType, GetEncoding(mimeType), stream);
            }
            catch
            {
                return new WebResourceResponse("text/plain", "UTF-8", new MemoryStream());
            }
        }

        public override void OnPageFinished(WebView? view, string? url)
        {
            base.OnPageFinished(view, url);
            activity.TryLoadSelectedFileInWebView();
        }

        private static string? GetEncoding(string mimeType)
        {
            return mimeType.StartsWith("text/", StringComparison.Ordinal) || mimeType == "application/json"
                ? "UTF-8"
                : null;
        }
    }
}

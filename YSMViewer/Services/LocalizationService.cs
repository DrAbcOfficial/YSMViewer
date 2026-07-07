using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Resources;

namespace YSMViewer.Services;

public sealed class LocalizationService
{
    private static readonly ILogger Logger = YsmLog.For(nameof(LocalizationService));
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture = CultureInfo.GetCultureInfo("en");

    public CultureInfo CurrentCulture
    {
        get => _currentCulture;
        private set
        {
            if (_currentCulture.Name == value.Name) return;
            _currentCulture = value;
            CultureInfo.CurrentUICulture = value;
            CultureChanged?.Invoke();
        }
    }

    public event Action? CultureChanged;

    public IReadOnlyList<(string Code, string Name)> SupportedCultures { get; } = new ReadOnlyCollection<(string, string)>(
    [
        ("en", "English"),
        ("zh", "中文"),
    ]);

    public LocalizationService()
    {
        _resourceManager = new ResourceManager("YSMViewer.Resources.Strings", typeof(LocalizationService).Assembly);
    }

    public string GetString(string key)
    {
        try { return _resourceManager.GetString(key, _currentCulture) ?? key; }
        catch (Exception ex) { Logger.LogWarning(ex, "Failed to get localized string for key '{Key}'", key); return key; }
    }

    public void SetLanguage(string code)
    {
        try
        {
            var culture = code switch
            {
                "zh" => CultureInfo.GetCultureInfo("zh-CN"),
                _ => CultureInfo.GetCultureInfo("en"),
            };
            CurrentCulture = culture;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to set language to '{Code}'", code);
        }
    }
}
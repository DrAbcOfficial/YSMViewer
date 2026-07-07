using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Svg.Skia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Svg.Model;
using YSMViewer.Rendering;
using YSMViewer.Services;
using YSMViewer.ViewModels;

namespace YSMViewer.Views.Shared;

public partial class ModelBottomBar : UserControl
{
    private static ThemeService ThemeSvc => App.Services.GetRequiredService<ThemeService>();

    public ModelBottomBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplyAllSvgColors();
        ThemeSvc.ModeChanged += OnThemeModeChanged;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ThemeSvc.ModeChanged -= OnThemeModeChanged;
    }

    private void OnThemeModeChanged(AppThemeMode mode) => ApplyAllSvgColors();

    private void ApplyAllSvgColors()
    {
        ApplySvgColor(CameraResetImg, "avares://YSMViewer/Assets/svg/origin.svg");
        ApplySvgColor(CameraFrontImg, "avares://YSMViewer/Assets/svg/up-junction.svg");
        ApplySvgColor(CameraSideImg, "avares://YSMViewer/Assets/svg/left-junction.svg");
        ApplySvgColor(CameraTopImg, "avares://YSMViewer/Assets/svg/down-junction.svg");
    }

    private static void ApplySvgColor(Image image, string svgPath)
    {
        var color = ThemeSvc.IsDarkTheme() ? "#8b949e" : "#656d76";
        try
        {
            var source = SvgSource.Load(svgPath, new Uri("avares://YSMViewer/"));
            source.ReLoad(new SvgParameters(null, $":root {{ color: {color}; }}"));
            image.Source = new SvgImage { Source = source };
        }
        catch (Exception ex)
        {
            YsmLog.For<ModelBottomBar>().LogWarning(ex, "Failed to load SVG icon '{Path}'", svgPath);
        }
    }

    private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        vm.IsAnimating = !vm.IsAnimating;
        PlayPauseIcon.Data = vm.IsAnimating
            ? Geometry.Parse("M6 4 L6 28 L12 28 L12 4 Z M18 4 L18 28 L24 28 L24 4 Z")
            : Geometry.Parse("M8 4 L8 28 L24 16 Z");
    }

    private void OnPreviousAnimationClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.PreviousAnimation();
    }

    private void OnNextAnimationClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm) vm.NextAnimation();
    }

    private void OnStopAnimationClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.StopAnimation();
            PlayPauseIcon.Data = Geometry.Parse("M8 4 L8 28 L24 16 Z");
        }
    }

    private void OnCameraResetClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Renderer is IInteractiveRenderer interactive)
            interactive.ResetCamera();
    }

    private void OnCameraFrontClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetCameraView(RenderCameraView.Front);
    }

    private void OnCameraSideClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetCameraView(RenderCameraView.Side);
    }

    private void OnCameraTopClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.Renderer.SetCameraView(RenderCameraView.Top);
    }
}

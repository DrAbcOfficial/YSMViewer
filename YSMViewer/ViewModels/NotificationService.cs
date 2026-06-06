using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Data.Converters;

namespace YSMViewer.ViewModels;

public enum NotificationType
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed partial class NotificationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message;

    [ObservableProperty]
    private NotificationType _type;

    [ObservableProperty]
    private bool _isDismissing;

    public NotificationViewModel(string message, NotificationType type = NotificationType.Info)
    {
        _message = message;
        _type = type;
    }
}

public sealed partial class NotificationService : ObservableObject
{
    public ObservableCollection<NotificationViewModel> Notifications { get; } = [];

    public void Show(string message, NotificationType type = NotificationType.Info, int durationMs = 3000)
    {
        var notification = new NotificationViewModel(message, type);
        Notifications.Add(notification);

        if (durationMs > 0)
        {
            _ = Task.Delay(durationMs).ContinueWith(_ =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Dismiss(notification)));
        }
    }

    [RelayCommand]
    private void Dismiss(NotificationViewModel notification)
    {
        Notifications.Remove(notification);
    }
}

public sealed class NotificationIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is NotificationType type)
            return type switch
            {
                NotificationType.Success => "\u2713",
                NotificationType.Warning => "\u26A0",
                NotificationType.Error => "\u2717",
                _ => "\u2139",
            };
        return "\u2139";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
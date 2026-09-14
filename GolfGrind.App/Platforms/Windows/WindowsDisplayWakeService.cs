using GolfGrind.App.Services;
using Windows.System.Display;

namespace GolfGrind.App.Platforms.Windows;

public sealed class WindowsDisplayWakeService : IDisplayWakeService
{
    private DisplayRequest? _request;

    public bool IsActive => _request is not null;

    public void SetActive(bool active)
    {
        if (active && _request is null)
        {
            var request = new DisplayRequest();
            request.RequestActive();
            _request = request;
        }
        else if (!active && _request is not null)
        {
            _request.RequestRelease();
            _request = null;
        }
    }

    public void Dispose() => SetActive(false);
}

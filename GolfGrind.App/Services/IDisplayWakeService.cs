namespace GolfGrind.App.Services;

public interface IDisplayWakeService : IDisposable
{
    bool IsActive { get; }
    void SetActive(bool active);
}

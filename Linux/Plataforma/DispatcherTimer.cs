namespace System.Windows.Threading;

/// <summary>
/// Substituto mínimo do DispatcherTimer do WPF, só para o AvisosService (compartilhado) compilar no Linux.
/// O Tick roda numa thread do pool; o AvisosService só mexe nos dados e em notificações.
/// </summary>
public sealed class DispatcherTimer
{
    private readonly Lock _trava = new();
    private Timer? _timer;
    private TimeSpan _intervalo = TimeSpan.FromSeconds(1);

    public event EventHandler? Tick;

    public TimeSpan Interval
    {
        get => _intervalo;
        set
        {
            _intervalo = value;
            _timer?.Change(value, value);
        }
    }

    public void Start() => _timer ??= new Timer(_ =>
    {
        lock (_trava) Tick?.Invoke(this, EventArgs.Empty);
    }, null, _intervalo, _intervalo);

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }
}

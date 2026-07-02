namespace Kasa.Api.Auth;

/// <summary>
/// Brute force koruması: 5 hatalı denemede 15 dakika kilit. Tek kullanıcılı uygulama
/// olduğundan sayaç global ve in-memory tutulur (IP bazlı ayrım gereksiz).
/// Zaman <see cref="TimeProvider"/> üzerinden okunur — testler kilit süresini bekleme
/// yapmadan simüle eder (Faz 4 altyapısı).
/// </summary>
public sealed class LoginRateLimiter(TimeProvider clock)
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private readonly Lock _sync = new();
    private int _failures;
    private DateTimeOffset? _lockedUntil;

    /// <summary>Kilit aktifse true; kilit süresi dolduysa sayaç sıfırlanır.</summary>
    public bool IsLockedOut(out DateTimeOffset lockedUntil)
    {
        lock (_sync)
        {
            if (_lockedUntil is { } until)
            {
                if (clock.GetUtcNow() < until)
                {
                    lockedUntil = until;
                    return true;
                }
                _lockedUntil = null;
                _failures = 0;
            }
            lockedUntil = default;
            return false;
        }
    }

    /// <summary>Hatalı denemeyi sayar; eşiğe ulaşıldıysa kilidi başlatıp bitişini döner.</summary>
    public (int Failures, DateTimeOffset? LockedUntil) RegisterFailure()
    {
        lock (_sync)
        {
            _failures++;
            if (_failures >= MaxFailures)
                _lockedUntil = clock.GetUtcNow() + LockDuration;
            return (_failures, _lockedUntil);
        }
    }

    /// <summary>Başarılı girişte sayaç temizlenir.</summary>
    public void Reset()
    {
        lock (_sync)
        {
            _failures = 0;
            _lockedUntil = null;
        }
    }
}

namespace ServerGuard.Api.Reputation;

/// <summary>
/// Bir IP adresinin dış itibar servisindeki kötüye kullanım skorunu döner.
/// </summary>
public interface IIpReputationService
{
    /// <summary>
    /// Skoru döner; bilinmiyorsa <c>null</c>. <b>Asla exception fırlatmaz ve asla süresiz beklemez.</b>
    /// Çağıran taraf, skorun gelmemesi durumunda kendi işine devam edebilmelidir.
    /// </summary>
    Task<int?> TryGetAbuseScoreAsync(string ipAddress, CancellationToken cancellationToken);
}

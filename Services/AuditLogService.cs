using DigitalEvidenceManagementSystem.Data;
using DigitalEvidenceManagementSystem.Models;

namespace DigitalEvidenceManagementSystem.Services;

public sealed class AuditLogService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditLogRepository _auditLogRepository;

    public AuditLogService(IHttpContextAccessor httpContextAccessor, AuditLogRepository auditLogRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _auditLogRepository = auditLogRepository;
    }

    public Task LogAsync(AppUser user, string eventType, string? entityType, string? entityId, string? details, bool succeeded = true, CancellationToken cancellationToken = default)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var userAgent = request?.Headers.UserAgent.ToString();

        return _auditLogRepository.AddAsync(
            user.UserId,
            user.Username,
            eventType,
            entityType,
            entityId,
            ipAddress,
            userAgent,
            details,
            succeeded,
            cancellationToken);
    }
}

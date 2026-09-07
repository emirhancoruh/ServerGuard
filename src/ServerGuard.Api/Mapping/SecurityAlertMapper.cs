using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Mapping;

public static class SecurityAlertMapper
{
    public static SecurityAlertDto ToDto(this SecurityAlert entity) => new(
        entity.Id,
        entity.ServerName,
        entity.AlertType,
        entity.Severity,
        entity.SourceIp,
        entity.ObservedCount,
        entity.Description,
        entity.Timestamp,
        entity.AbuseConfidenceScore);
}

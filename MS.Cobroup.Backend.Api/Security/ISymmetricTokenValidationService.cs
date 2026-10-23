using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.Backend.Api.Security;

/// <summary>
/// Abstracción para la validación local de tokens JWT simétricos HS256 en MS.Cobroup.Backend.
/// Este servicio es exclusivamente validador; nunca emite tokens ni contiene capacidades de firma.
/// </summary>
public interface ISymmetricTokenValidationService : IDisposable
{
    /// <summary>
    /// Identificador público no secreto de la clave de validación activa del ambiente (kid).
    /// </summary>
    string ActiveKeyId { get; }

    /// <summary>
    /// Claves simétricas disponibles para validación (activa y claves anteriores de rotación).
    /// </summary>
    IReadOnlyCollection<SecurityKey> ValidationKeys { get; }

    /// <summary>
    /// Parámetros estándar de validación con allowlist estricta para HS256.
    /// </summary>
    TokenValidationParameters TokenValidationParameters { get; }
}

using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace MS.Cobroup.Backend.Api.Security;

/// <summary>
/// Servicio de validación local de tokens JWT HS256 para el microservicio operativo (Backend).
/// Consume el secreto simétrico del ambiente para verificar firmas de tokens emitidos por BackendAdmin.
/// Nunca emite tokens ni expone credenciales de firma.
/// </summary>
public sealed class SymmetricTokenValidationService : ISymmetricTokenValidationService
{
    private readonly SymmetricSecurityKey activeKey;
    private readonly Dictionary<string, SymmetricSecurityKey> previousKeys = new(StringComparer.Ordinal);
    private readonly byte[] activeKeyRawBytes;
    private readonly List<byte[]> previousKeysRawBytes = [];
    private bool disposed;

    public string ActiveKeyId { get; }

    public IReadOnlyCollection<SecurityKey> ValidationKeys
    {
        get
        {
            ThrowIfDisposed();
            var keys = new List<SecurityKey> { activeKey };
            keys.AddRange(previousKeys.Values);
            return keys.AsReadOnly();
        }
    }

    public TokenValidationParameters TokenValidationParameters { get; }

    public SymmetricTokenValidationService(IConfiguration configuration, IHostEnvironment environment)
    {
        var rawKey = configuration["Jwt:SigningKeyBase64"];
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            throw new InvalidOperationException(
                "La clave de firma simétrica 'Jwt:SigningKeyBase64' es obligatoria y no puede ser nula ni vacía.");
        }

        rawKey = rawKey.Trim();
        if (IsPlaceholder(rawKey))
        {
            throw new InvalidOperationException(
                "La clave 'Jwt:SigningKeyBase64' contiene un valor de marcador de posición (placeholder) no permitido.");
        }

        try
        {
            activeKeyRawBytes = Convert.FromBase64String(rawKey);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "La clave 'Jwt:SigningKeyBase64' no es una cadena Base64 válida.");
        }

        if (activeKeyRawBytes.Length < 32)
        {
            throw new InvalidOperationException(
                $"La clave 'Jwt:SigningKeyBase64' debe tener una longitud mínima de 32 bytes (256 bits). Longitud obtenida: {activeKeyRawBytes.Length} bytes.");
        }

        ActiveKeyId = configuration["Jwt:ActiveKeyId"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(ActiveKeyId))
        {
            ActiveKeyId = "k1";
        }

        activeKey = new SymmetricSecurityKey(activeKeyRawBytes)
        {
            KeyId = ActiveKeyId
        };

        // Cargar claves anteriores si existen para la ventana de rotación
        LoadPreviousKeys(configuration);

        var issuer = configuration["Identity:Issuer"]
            ?? throw new InvalidOperationException("Identity:Issuer is required.");
        var audience = configuration["Identity:Audience"] ?? "cobroup-api";

        if (!environment.IsDevelopment())
        {
            if (!issuer.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Identity:Issuer debe ser una URL HTTPS válida fuera de Development. Valor actual: '{issuer}'");
            }

            if (!string.Equals(audience, "cobroup-api", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Identity:Audience debe ser 'cobroup-api'. Valor actual: '{audience}'");
            }
        }

        TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RoleClaimType = "role",
            NameClaimType = "preferred_username",
            IssuerSigningKeyResolver = ResolveSigningKey
        };
    }

    private void LoadPreviousKeys(IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt:PreviousValidationKeys");
        if (!section.Exists())
        {
            return;
        }

        foreach (var child in section.GetChildren())
        {
            var kid = child["KeyId"]?.Trim();
            var rawPrevKey = child["SigningKeyBase64"]?.Trim();

            if (string.IsNullOrWhiteSpace(kid) || string.IsNullOrWhiteSpace(rawPrevKey))
            {
                continue;
            }

            if (IsPlaceholder(rawPrevKey))
            {
                throw new InvalidOperationException(
                    $"La clave anterior con kid '{kid}' contiene un marcador de posición (placeholder) no permitido.");
            }

            byte[] prevBytes;
            try
            {
                prevBytes = Convert.FromBase64String(rawPrevKey);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException(
                    $"La clave anterior con kid '{kid}' no es una cadena Base64 válida.");
            }

            if (prevBytes.Length < 32)
            {
                throw new InvalidOperationException(
                    $"La clave anterior con kid '{kid}' debe tener una longitud mínima de 32 bytes. Longitud obtenida: {prevBytes.Length} bytes.");
            }

            previousKeysRawBytes.Add(prevBytes);
            previousKeys[kid] = new SymmetricSecurityKey(prevBytes)
            {
                KeyId = kid
            };
        }
    }

    private IEnumerable<SecurityKey> ResolveSigningKey(
        string token,
        SecurityToken securityToken,
        string kid,
        TokenValidationParameters validationParameters)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(kid))
        {
            return [];
        }

        if (string.Equals(activeKey.KeyId, kid, StringComparison.Ordinal))
        {
            return [activeKey];
        }

        if (previousKeys.TryGetValue(kid, out var previousKey))
        {
            return [previousKey];
        }

        return [];
    }

    private static bool IsPlaceholder(string value)
    {
        return value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("${{", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;

        Array.Clear(activeKeyRawBytes, 0, activeKeyRawBytes.Length);
        foreach (var bytes in previousKeysRawBytes)
        {
            Array.Clear(bytes, 0, bytes.Length);
        }
    }
}

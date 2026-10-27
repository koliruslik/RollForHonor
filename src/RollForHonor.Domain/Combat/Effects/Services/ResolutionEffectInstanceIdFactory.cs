using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using RollForHonor.Domain.Combat.Effects.Models;
using RollForHonor.Domain.Combat.State.Models;

namespace RollForHonor.Domain.Combat.Effects.Services;

/// <summary>Derives instance identities from action, recipient, definition and payload index.</summary>
public sealed class ResolutionEffectInstanceIdFactory : IEffectInstanceIdFactory
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">A participant or definition identity is null.</exception>
    /// <exception cref="ArgumentException">The resolution identity is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The occurrence is negative.</exception>
    public EffectInstanceId Create(Guid resolutionId, CombatantId recipientId,
        EffectDefinitionId definitionId, int occurrence)
    {
        if (resolutionId == Guid.Empty)
            throw new ArgumentException("A resolution identity cannot be empty.", nameof(resolutionId));
        ArgumentNullException.ThrowIfNull(recipientId);
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(occurrence);
        var key = $"{resolutionId:D}/{recipientId.Value:D}/{definitionId.Value:D}/"
            + occurrence.ToString(CultureInfo.InvariantCulture);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        bytes[7] = (byte)((bytes[7] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new EffectInstanceId(new Guid(bytes.AsSpan(0, 16)));
    }
}

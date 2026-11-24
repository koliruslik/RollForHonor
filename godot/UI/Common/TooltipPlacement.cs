using System.Linq;
using Godot;

namespace RollForHonor.Godot.UI.Common;

/// <summary>Places hover cards beside their source while keeping the pointer and target uncovered.</summary>
public static class TooltipPlacement
{
    /// <summary>Maps the current hovered control to the tooltip parent's local coordinates.</summary>
    /// <param name="parent">The design-space tooltip parent.</param>
    /// <param name="cursor">The pointer in the same local space.</param>
    /// <returns>The hovered source rectangle, or a small pointer exclusion rectangle.</returns>
    public static Rect2 SourceBounds(Control parent, Vector2 cursor)
    {
        var hovered = parent.GetViewport().GuiGetHoveredControl();

        if (hovered is not null)
        {
            var transform = parent.GetGlobalTransformWithCanvas().AffineInverse() * hovered.GetGlobalTransformWithCanvas();
            var start = transform * Vector2.Zero;
            var end = transform * hovered.Size;
            return new Rect2(start, end - start).Abs();
        }

        return new Rect2(cursor - Vector2.One * 6, Vector2.One * 12);
    }

    /// <summary>Chooses a fitting side of the hovered source, with a least-overlap fallback for constrained viewports.</summary>
    /// <param name="bounds">Available design-space width and height.</param>
    /// <param name="size">The rendered tooltip extent.</param>
    /// <param name="cursor">The local pointer position.</param>
    /// <param name="source">The hovered item/property rectangle.</param>
    /// <returns>A clamped top-left position which avoids the source whenever space permits.</returns>
    public static Vector2 Place(Vector2 bounds, Vector2 size, Vector2 cursor, Rect2 source)
    {
        const float gap = 14;
        var maximum = (bounds - size).Max(Vector2.Zero);
        var vertical = Mathf.Clamp(cursor.Y - 20, 0, maximum.Y);
        var horizontal = Mathf.Clamp(cursor.X - size.X / 2, 0, maximum.X);
        Vector2[] candidates =
        [
            new(source.End.X + gap, vertical),
            new(source.Position.X - size.X - gap, vertical),
            new(horizontal, source.End.Y + gap),
            new(horizontal, source.Position.Y - size.Y - gap)
        ];
        var viewport = new Rect2(Vector2.Zero, bounds);

        foreach (var candidate in candidates)
        {
            var rect = new Rect2(candidate, size);

            if (viewport.Encloses(rect) && !rect.Intersects(source) && !rect.Grow(6).HasPoint(cursor))
            {
                return candidate;
            }
        }

        return candidates
            .Select(candidate => candidate.Clamp(Vector2.Zero, maximum))
            .OrderBy(candidate => Overlap(new Rect2(candidate, size), source)
                + (new Rect2(candidate, size).Grow(6).HasPoint(cursor) ? bounds.X * bounds.Y : 0))
            .First();
    }

    private static float Overlap(Rect2 first, Rect2 second)
    {
        var intersection = first.Intersection(second);

        return intersection.Size.X * intersection.Size.Y;
    }
}

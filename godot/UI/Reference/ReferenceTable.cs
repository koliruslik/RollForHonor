using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using RollForHonor.Godot.UI.Common;
using RollForHonor.Godot.UI.Localization;

namespace RollForHonor.Godot.UI.Reference;

/// <summary>Captures a readable table column without binding it to a particular control.</summary>
/// <param name="Title">The translatable heading.</param>
/// <param name="Width">The relative width in design pixels.</param>
/// <param name="Details">Optional linked explanation of the column.</param>
public sealed record ReferenceColumn(string Title, int Width, InspectionTopic Details = null);

/// <summary>Captures one colored row and optional navigation or inspection action.</summary>
/// <param name="Id">The stable row identity.</param>
/// <param name="Cells">Plain translatable cell values in column order.</param>
/// <param name="Color">The row's rarity or semantic accent.</param>
/// <param name="FilterTags">Pipe-separated filter identities.</param>
/// <param name="SearchText">Additional searchable names and explanations.</param>
/// <param name="Details">Optional captured explanation opened on row selection.</param>
/// <param name="CellDetails">Optional per-cell explanations with further semantic links.</param>
public sealed record ReferenceTableRow(string Id, IReadOnlyList<string> Cells, Color Color,
    string FilterTags = "", string SearchText = "", InspectionTopic Details = null, IReadOnlyList<InspectionTopic> CellDetails = null)
{
    /// <summary>Tests all search words against the row and an optional exact filter tag.</summary>
    /// <param name="query">Whitespace-separated search words.</param>
    /// <param name="tag">An exact filter identity, or empty for every row.</param>
    /// <returns>True when both search and filter match.</returns>
    public bool Matches(string query, string tag)
    {
        var text = string.Join(" ", Cells.Select(L.Refresh)) + " " + Id + " " + SearchText;

        return (string.IsNullOrEmpty(tag) || FilterTags.Split('|').Contains(tag, StringComparer.Ordinal))
            && (query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .All(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Owns copied table data shared by title-screen and in-session reference readers.</summary>
public sealed class ReferenceTable
{
    /// <summary>Gets ordered columns.</summary>
    public IReadOnlyList<ReferenceColumn> Columns { get; }

    /// <summary>Gets immutable captured rows.</summary>
    public IReadOnlyList<ReferenceTableRow> Rows { get; }

    /// <summary>Gets exact filter identities, translated only for display.</summary>
    public IReadOnlyList<string> Filters { get; }

    /// <summary>Gets selectable tier tables; absent for ordinary catalogs.</summary>
    public IReadOnlyList<ReferenceTable> Tiers { get; }

    /// <summary>Copies a table with consistent cell counts and positive column widths.</summary>
    /// <param name="columns">Nonempty ordered columns.</param>
    /// <param name="rows">Rows with one cell per column.</param>
    /// <param name="filters">Optional filter identities.</param>
    /// <param name="tiers">Optional tables ordered T7 through T1, sharing the row/filter contract.</param>
    /// <exception cref="ArgumentNullException">A required collection is null.</exception>
    /// <exception cref="ArgumentException">A width or row shape is invalid.</exception>
    public ReferenceTable(IEnumerable<ReferenceColumn> columns, IEnumerable<ReferenceTableRow> rows, IEnumerable<string> filters = null, IEnumerable<ReferenceTable> tiers = null)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        var headings = columns.ToArray();
        var values = rows.ToArray();

        if (headings.Length == 0 || headings.Any(column => column is null || column.Width <= 0)
            || values.Any(row => row is null || row.Cells is null || row.Cells.Count != headings.Length || row.CellDetails is not null && row.CellDetails.Count != headings.Length))
        {
            throw new ArgumentException("A reference table needs positive-width columns and matching row cells.");
        }

        Columns = Array.AsReadOnly(headings);
        Rows = Array.AsReadOnly(values
            .Select(row => row with { Cells = Array.AsReadOnly(row.Cells.ToArray()), CellDetails = row.CellDetails is null ? null : Array.AsReadOnly(row.CellDetails.ToArray()) })
            .ToArray());
        Tiers = Array.AsReadOnly((tiers ?? []).ToArray());
        Filters = Array.AsReadOnly((filters ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }
}

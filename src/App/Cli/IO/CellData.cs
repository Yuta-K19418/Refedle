namespace Refedle.App.Cli.IO;

/// <summary>
///  Whether a CLI batch cell carries a usable value and, when it does not, why
///  (explicit null, an absent property, or an unreadable source). Replaces the
///  former "&lt;null&gt;"/"&lt;error&gt;" string sentinels with an explicit signal.
/// </summary>
internal enum CellPresence
{
    Value,
    Null,
    Missing,
    Invalid,
}

/// <summary>
///  How an <see cref="IRecordWriter"/> must serialize a <see cref="CellData"/>'s
///  <see cref="CellData.Value"/> — a purely syntactic decision, independent of the
///  cell's domain type (ColumnType is not involved anywhere in the batch path).
/// </summary>
internal enum CellEncoding
{
    PlainText,
    Raw,
    Numeric,
    Boolean,
}

/// <summary>
///  A single CLI batch cell: its text plus the presence/encoding signals the
///  writer needs to emit it correctly. Passed between <see cref="IRecordReader"/>
///  and <see cref="IRecordWriter"/> in place of the former bare
///  <see cref="ReadOnlySpan{T}"/> of <see langword="char"/>.
/// </summary>
internal readonly ref struct CellData(
    ReadOnlySpan<char> value,
    CellPresence presence,
    CellEncoding encoding = CellEncoding.PlainText)
{
    public ReadOnlySpan<char> Value { get; } = value;

    public CellPresence Presence { get; } = presence;

    public CellEncoding Encoding { get; } = encoding;
}

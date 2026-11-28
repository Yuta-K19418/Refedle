using Refedle.Engine.Types;

namespace Refedle.App.Cli.IO;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
internal sealed class RecordReaderAttribute(DataFormat format) : Attribute
{
    public DataFormat Format { get; } = format;
}

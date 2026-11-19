using System.Diagnostics.CodeAnalysis;

// Shared test helpers are test infrastructure, not product code; this attribute keeps
// the whole assembly out of coverage reports without touching CI or runsettings files.
[assembly: ExcludeFromCodeCoverage]

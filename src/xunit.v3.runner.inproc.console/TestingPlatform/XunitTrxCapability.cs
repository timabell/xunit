using Microsoft.Testing.Extensions.TrxReport.Abstractions;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal sealed class XunitTrxCapability : ITrxReportCapability
{
	public bool IsTrxEnabled { get; set; }

	public bool IsSupported =>
		true;

	public void Enable() =>
		IsTrxEnabled = true;
}

using Microsoft.Testing.Platform.Logging;
using Xunit.Runner.Common;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal sealed class LoggerRunnerLogger(ILogger logger) :
	IRunnerLogger
{
	public object LockObject { get; } = new();

	public void LogError(
		StackFrameInfo stackFrame,
		string message) =>
			logger.LogError(message);

	public void LogImportantMessage(
		StackFrameInfo stackFrame,
		string message) =>
			logger.LogInformation(message);

	public void LogMessage(
		StackFrameInfo stackFrame,
		string message) =>
			logger.LogInformation(message);

	public void LogRaw(string message) =>
		logger.LogInformation(message);

	public void LogWarning(
		StackFrameInfo stackFrame,
		string message) =>
			logger.LogWarning(message);

	public void WaitForAcknowledgment()
	{ }
}

using Microsoft.Testing.Platform.Logging;
using Xunit.Runner.Common;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

/// <summary>
/// An implementation of <see cref="IRunnerLogger"/> that delegates to <see cref="ILogger"/>.
/// </summary>
public class LoggerRunnerLogger(ILogger logger) :
	IRunnerLogger
{
	/// <inheritdoc/>
	public object LockObject { get; } = new();

	/// <inheritdoc/>
	public void LogError(
		StackFrameInfo stackFrame,
		string message) =>
			logger.LogError(message);

	/// <inheritdoc/>
	public void LogImportantMessage(
		StackFrameInfo stackFrame,
		string message) =>
			logger.LogInformation(message);

	/// <inheritdoc/>
	public void LogMessage(
		StackFrameInfo stackFrame,
		string message) =>
			logger.LogInformation(message);

	/// <inheritdoc/>
	public void LogRaw(string message) =>
		logger.LogInformation(message);

	/// <inheritdoc/>
	public void LogWarning(
		StackFrameInfo stackFrame,
		string message) =>
			logger.LogWarning(message);

	/// <inheritdoc/>
	public void WaitForAcknowledgment()
	{ }
}

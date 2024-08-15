using Microsoft.Testing.Platform.Logging;
using Xunit.Sdk;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

/// <summary>
/// An implementation of <see cref="IMessageSink"/> to log diagnostic messages to the Microsoft.Testing.Platform logger.
/// </summary>
/// <param name="logger">The logger</param>
/// <param name="diagnosticMessages">The flag to indicate if we should report diagnostic messages</param>
/// <param name="internalDiagnosticMessages">The flag to indicate if we should report internal diagnostic messages</param>
public class LoggerDiagnosticMessageSink(
	ILogger logger,
	bool diagnosticMessages,
	bool internalDiagnosticMessages) :
		IMessageSink
{
	/// <inheritdoc/>
	public bool OnMessage(IMessageSinkMessage message)
	{
		if (diagnosticMessages && message is IDiagnosticMessage diagnosticMessage)
			logger.LogInformation(diagnosticMessage.Message);
		else if (internalDiagnosticMessages && message is IInternalDiagnosticMessage internalDiagnosticMessage)
			logger.LogInformation(internalDiagnosticMessage.Message);

		return true;
	}

	/// <summary>
	/// Factory method to create the diagnostic message sink.
	/// </summary>
	public static IMessageSink? TryCreate(
		ILogger logger,
		bool diagnosticMessages,
		bool internalDiagnosticMessages) =>
			diagnosticMessages || internalDiagnosticMessages
				? new LoggerDiagnosticMessageSink(logger, diagnosticMessages, internalDiagnosticMessages)
				: null;
}

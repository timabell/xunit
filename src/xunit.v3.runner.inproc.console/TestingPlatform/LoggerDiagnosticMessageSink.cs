using Microsoft.Testing.Platform.Logging;
using Xunit.Sdk;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal sealed class LoggerDiagnosticMessageSink(
	ILogger logger,
	bool diagnosticMessages,
	bool internalDiagnosticMessages) :
		IMessageSink
{
	public bool OnMessage(IMessageSinkMessage message)
	{
		if (diagnosticMessages && message is IDiagnosticMessage diagnosticMessage)
			logger.LogInformation(diagnosticMessage.Message);
		else if (internalDiagnosticMessages && message is IInternalDiagnosticMessage internalDiagnosticMessage)
			logger.LogInformation(internalDiagnosticMessage.Message);

		return true;
	}

	public static IMessageSink? TryCreate(
		ILogger logger,
		bool diagnosticMessages,
		bool internalDiagnosticMessages) =>
			diagnosticMessages || internalDiagnosticMessages
				? new LoggerDiagnosticMessageSink(logger, diagnosticMessages, internalDiagnosticMessages)
				: null;
}

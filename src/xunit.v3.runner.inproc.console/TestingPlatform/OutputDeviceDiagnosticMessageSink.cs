using System;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Xunit.Sdk;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal sealed class OutputDeviceDiagnosticMessageSink(
	IOutputDevice outputDevice,
	bool diagnosticMessages,
	bool internalDiagnosticMessages,
	IMessageSink innerSink) :
		OutputDeviceDataProducerBase("output device diagnostic message sink", "e85050db-8ef2-4ef1-895b-8c4b620025e2"), IMessageSink
{
	public bool OnMessage(IMessageSinkMessage message)
	{
		if (diagnosticMessages && message is IDiagnosticMessage diagnosticMessage)
			outputDevice.DisplayAsync(this, ToMessageWithColor(diagnosticMessage.Message, ConsoleColor.Yellow)).SpinWait();
		if (internalDiagnosticMessages && message is IInternalDiagnosticMessage internalDiagnosticMessage)
			outputDevice.DisplayAsync(this, ToMessageWithColor(internalDiagnosticMessage.Message, ConsoleColor.DarkGray)).SpinWait();

		return innerSink.OnMessage(message);
	}

	public static IMessageSink? TryCreate(
		ILogger logger,
		IOutputDevice outputDevice,
		bool diagnosticMessages,
		bool internalDiagnosticMessages)
	{
		var innerSink = LoggerDiagnosticMessageSink.TryCreate(logger, diagnosticMessages, internalDiagnosticMessages);
		if (innerSink is null)
			return null;

		return new OutputDeviceDiagnosticMessageSink(outputDevice, diagnosticMessages, internalDiagnosticMessages, innerSink);
	}
}

using System;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal abstract class OutputDeviceDataProducerBase(
	string componentName,
	string uid) :
		ExtensionBase(componentName, uid), IOutputDeviceDataProducer
{
	protected static IOutputDeviceData ToMessageWithColor(
		string message,
		ConsoleColor color) =>
			new FormattedTextOutputDeviceData(message) { ForegroundColor = new SystemConsoleColor { ConsoleColor = color } };
}

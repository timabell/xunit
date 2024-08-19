using System;
using Microsoft.Testing.Platform.OutputDevice;
using Xunit.Runner.Common;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal sealed class OutputDeviceRunnerLogger(
	IOutputDevice outputDevice,
	IRunnerLogger? innerLogger) :
		OutputDeviceDataProducerBase("output device runner logger", "b7b01fae-f36c-492e-b561-b3e4cad62203"), IRunnerLogger
{
	public object LockObject { get; } = new();

	public void LogError(
		StackFrameInfo stackFrame,
		string message)
	{
		outputDevice.DisplayAsync(this, ToMessageWithColor(message, ConsoleColor.Red)).SpinWait();
		innerLogger?.LogError(stackFrame, message);
	}

	public void LogImportantMessage(
		StackFrameInfo stackFrame,
		string message)
	{
		outputDevice.DisplayAsync(this, ToMessageWithColor(message, ConsoleColor.Gray)).SpinWait();
		innerLogger?.LogImportantMessage(stackFrame, message);
	}

	public void LogMessage(
		StackFrameInfo stackFrame,
		string message)
	{
		outputDevice.DisplayAsync(this, ToMessageWithColor(message, ConsoleColor.DarkGray)).SpinWait();
		innerLogger?.LogMessage(stackFrame, message);
	}

	public void LogRaw(string message)
	{
		outputDevice.DisplayAsync(this, new TextOutputDeviceData(message)).SpinWait();
		innerLogger?.LogRaw(message);
	}

	public void LogWarning(
		StackFrameInfo stackFrame,
		string message)
	{
		outputDevice.DisplayAsync(this, ToMessageWithColor(message, ConsoleColor.Yellow)).SpinWait();
		innerLogger?.LogWarning(stackFrame, message);
	}

	public void WaitForAcknowledgment()
	{ }
}

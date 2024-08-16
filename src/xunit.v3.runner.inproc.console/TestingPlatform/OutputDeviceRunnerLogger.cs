using System;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.OutputDevice;
using Xunit.Runner.Common;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

/// <summary>
/// An implementation of <see cref="IRunnerLogger"/> that delegates to <see cref="IOutputDevice"/>.
/// </summary>
public class OutputDeviceRunnerLogger(IOutputDevice outputDevice) :
	IRunnerLogger, IOutputDeviceDataProducer
{
	/// <inheritdoc/>
	public string Description =>
		"xUnit.net v3 Microsoft.Testing.Platform output device runner logger";

	/// <inheritdoc/>
	public string DisplayName =>
		Description;

	/// <inheritdoc/>
	public object LockObject { get; } = new();

	/// <inheritdoc/>
	public string Uid =>
		"b7b01fae-f36c-492e-b561-b3e4cad62203";

	/// <inheritdoc/>
	public string Version =>
		ThisAssembly.AssemblyVersion;

	/// <inheritdoc/>
	public Task<bool> IsEnabledAsync() =>
		Task.FromResult(true);

	/// <inheritdoc/>
	public void LogError(
		StackFrameInfo stackFrame,
		string message) =>
			outputDevice.DisplayAsync(this, ToMessageWithColor(message, ConsoleColor.Red)).SpinWait();

	/// <inheritdoc/>
	public void LogImportantMessage(
		StackFrameInfo stackFrame,
		string message) =>
			outputDevice.DisplayAsync(this, ToMessageWithColor(message, ConsoleColor.Gray)).SpinWait();

	/// <inheritdoc/>
	public void LogMessage(
		StackFrameInfo stackFrame,
		string message) =>
			outputDevice.DisplayAsync(this, ToMessageWithColor(message, ConsoleColor.DarkGray)).SpinWait();

	/// <inheritdoc/>
	public void LogRaw(string message) =>
		outputDevice.DisplayAsync(this, new TextOutputDeviceData(message)).SpinWait();

	/// <inheritdoc/>
	public void LogWarning(
		StackFrameInfo stackFrame,
		string message) =>
			outputDevice.DisplayAsync(this, ToMessageWithColor(message, ConsoleColor.Yellow)).SpinWait();

	static IOutputDeviceData ToMessageWithColor(
		string message,
		ConsoleColor color) =>
			new FormattedTextOutputDeviceData(message) { ForegroundColor = new SystemConsoleColor { ConsoleColor = color } };

	/// <inheritdoc/>
	public void WaitForAcknowledgment()
	{ }
}

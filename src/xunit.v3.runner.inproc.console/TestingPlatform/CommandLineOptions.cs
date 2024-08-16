using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal sealed class CommandLineOptions : ICommandLineOptionsProvider
{
	public string Description =>
		"xUnit.net v3 Microsoft.Testing.Platform command line options provider";

	public string DisplayName =>
		Description;

	public string Uid =>
		"7e0a6fd0-3615-48b0-859c-6bb4f51c3095";

	public string Version =>
		ThisAssembly.AssemblyVersion;

	public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
	[
		new CommandLineOption(
			"xunit-info",
			"Show xUnit.net information while running tests",
			ArgumentArity.Zero,
			false
		),
	];

	public Task<bool> IsEnabledAsync() =>
		Task.FromResult(true);

	public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) =>
		ValidationResult.ValidTask;

	public Task<ValidationResult> ValidateOptionArgumentsAsync(
		CommandLineOption commandOption,
		string[] arguments) =>
			ValidationResult.ValidTask;
}

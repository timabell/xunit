using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal sealed class CommandLineOptions() :
	ExtensionBase("command line options provider", "7e0a6fd0-3615-48b0-859c-6bb4f51c3095"), ICommandLineOptionsProvider
{
	public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
	[
		new CommandLineOption(
			"culture",
			"Run tests under the given culture. The available values are: 'default', 'invariant', and any system culture (i.e., 'en-US')",
			ArgumentArity.ExactlyOne,
			false
		),
		new CommandLineOption(
			"xunit-info",
			"Show xUnit.net information while running tests",
			ArgumentArity.Zero,
			false
		),
	];

	public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) =>
		ValidationResult.ValidTask;

	public Task<ValidationResult> ValidateOptionArgumentsAsync(
		CommandLineOption commandOption,
		string[] arguments)
	{
		if (commandOption.Name == "culture")
		{
			var culture = arguments[0];

			switch (culture.ToUpperInvariant())
			{
				case "DEFAULT":
				case "INVARIANT":
					break;

				default:
					try
					{
						CultureInfo.GetCultureInfo(culture);
					}
					catch (CultureNotFoundException)
					{
						return ValidationResult.InvalidTask(string.Format(CultureInfo.CurrentCulture, "Culture '{0}' is not valid", culture));
					}
					break;
			}
		}

		return ValidationResult.ValidTask;
	}
}

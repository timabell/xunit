using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Xunit.Internal;
using Xunit.Runner.Common;
using Xunit.Sdk;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal sealed class CommandLineOptionsProvider() :
	ExtensionBase("command line options provider", "7e0a6fd0-3615-48b0-859c-6bb4f51c3095"), ICommandLineOptionsProvider
{
	static readonly Dictionary<string, (string Description, ArgumentArity Arity, Action<string[], TestProjectConfiguration, TestAssemblyConfiguration> Parse)> options = new(StringComparer.OrdinalIgnoreCase)
	{
		// General options
		{ "culture", ("Run tests under the given culture. The available values are: 'default' (system culture) [default], 'invariant' (invariant culture), and any system culture (i.e., 'en-US').", ArgumentArity.ExactlyOne, OnCulture) },
		{ "explicit", ("Change the way explicit tests are handled. The available values are: 'off' (only run non-explicit tests) [default], 'on' (run both explicit and non-explicit tests), and 'only' (only run explicit tests).", ArgumentArity.ExactlyOne, OnExplicit) },
		{ "fail-skips", ("Change the way skipped tests are handled. The available values are: 'off' (report as skipped) [default], 'on' (reported as failed).", ArgumentArity.ExactlyOne, OnFailSkips) },
		{ "fail-warns", ("Change the way passing tests with warnings are handled. The available values are: 'off' (report as passing) [default], 'on' (reported as failed).", ArgumentArity.ExactlyOne, OnFailWarns) },
		{ "max-threads", ("Set maximum thread count for collection parallelization. The available values are: 'default' (1 thread per CPU thread) [default], 'unlimited' (run with unbounded thread count), '(integer)' (use exactly this many threads; i.e., '2' = 2 threads), '(float)x' (use a multiple of CPU threads; i.e., '2.0x' = 2.0 * CPU threads).", ArgumentArity.ExactlyOne, OnMaxThreads) },
		{ "method-display", ("Set default test display name. The available values are: 'classAndMethod' (use a fully qualified name) [default], 'method' (use just the method name).", ArgumentArity.ExactlyOne, OnMethodDisplay) },
		{ "method-display-options", ("Alters the default test display name. The available values are: 'none' (no altertions) [default], 'all' (all alterations), or one of more of: 'replacePeriodWithComma' (replace periods in names with commas), 'replaceUnderscoreWithSpace' (replace underscores in names with spaces), 'useOperatorMonikers' (replace 'lt' with '<', 'le' with '<=', 'eq' with '=', 'ne' with '!=', 'gt' with '>', and 'ge' with '>='), 'useEscapeSequences' (replace ASCII and Unicode escape sequences; i.e., 'X2C' becomes ',', 'U0192' becomes '\u0192').", ArgumentArity.OneOrMore, OnMethodDisplayOptions) },
		{ "parallel", ("Change test parallelization. The available values are: 'none' (turn off parallelization), 'collections' (parallelize by test collection) [default].", ArgumentArity.ExactlyOne, OnParallel) },
		{ "parallel-algorithm", ("Change the parallelization algorithm. The available values are: 'conservative' (start the minimum number of tests) [default], 'aggressive' (start as many tests as possible).", ArgumentArity.ExactlyOne, OnParallelAlgorithm) },
		{ "pre-enumerate-theories", ("Change theory pre-enumeration during discovery. The available values are: 'on' [default], 'off'.", ArgumentArity.ExactlyOne, OnPreEnumerateTheories) },
		{ "seed", ("Set the randomization seed. Pass an integer seed value.", ArgumentArity.ExactlyOne, OnSeed) },
		{ "show-live-output", ("Determine whether to show test output (from ITestOutputHelper) live during test execution. The available values are: 'on', 'off' [default]", ArgumentArity.ExactlyOne, OnShowLiveOutput) },
		{ "stop-on-fail", ("Stop running tests after the first test failure. The available values are: 'on', 'off' [default]", ArgumentArity.ExactlyOne, OnStopOnFail) },
		{ "xunit-diagnostics", ("Determine whether to show diagnostic messages. The available values are: 'on', 'off' [default]", ArgumentArity.ExactlyOne, OnDiagnostics) },
		{ "xunit-internal-diagnostics", ("Determine whether to show internal diagnostic messages. The available values are: 'on', 'off' [default]", ArgumentArity.ExactlyOne, OnInternalDiagnostics) },

		// Filtering
		{ "filter-class", ("Run all methods in a given test class. Pass one or more fully qualified type names (i.e., 'MyNamespace.MyClass' or 'MyNamespace.MyClass+InnerClass'). Specifying more than one is an OR operation.", ArgumentArity.OneOrMore, (arguments, _, assemblyConfig) => OnFilter(arguments, assemblyConfig.Filters.IncludedClasses)) },
		{ "filter-not-class", ("Do not run any methods in the given test class. Pass one or more fully qualified type names (i.e., 'MyNamspace.MyClass', or 'MyNamspace.MyClass+InnerClass'). Specifying more than one is an AND operation.", ArgumentArity.OneOrMore, (arguments, _, assemblyConfig) => OnFilter(arguments, assemblyConfig.Filters.ExcludedClasses)) },
		{ "filter-method", ("Run a given test method. Pass one ore more fully qualified method names or wildcards (i.e. 'MyNamespace.MyClass.MyTestMethod' or '*.MyTestMethod'). Specifying more than one is an OR operation.", ArgumentArity.OneOrMore, (arguments, _, assemblyConfig) => OnFilter(arguments, assemblyConfig.Filters.IncludedMethods)) },
		{ "filter-not-method", ("Do not run a given test method. Pass one ore more fully qualified method names or wildcards (i.e., 'MyNamspace.MyClass.MyTestMethod', or '*.MyTestMethod'). Specifying more than one is an AND operation.", ArgumentArity.OneOrMore, (arguments, _, assemblyConfig) => OnFilter(arguments, assemblyConfig.Filters.ExcludedMethods)) },
		{ "filter-namespace", ("Run all methods in the given namespace. Pass one or more namespaces (i.e. 'MyNamespace' or 'MyNamespace.MySubNamespace'). Specifying more than one is an OR operation.", ArgumentArity.OneOrMore, (arguments, _, assemblyConfig) => OnFilter(arguments, assemblyConfig.Filters.IncludedNamespaces)) },
		{ "filter-not-namespace", ("Do not run any methods in the given namespace. Pass one or more namespaces (i.e. 'MyNamespace' or 'MyNamespace.MySubNamespace'). Specifying more than one is an AND operation.", ArgumentArity.OneOrMore, (arguments, _, assemblyConfig) => OnFilter(arguments, assemblyConfig.Filters.ExcludedNamespaces)) },
		{ "filter-trait", ("Run all methods with a given trait value. Pass one or more name/value pairs (i.e. 'name=value'). Specifying more than one is an OR operation.", ArgumentArity.OneOrMore, (arguments, _, assemblyConfig) => OnFilterTrait(arguments, assemblyConfig.Filters.IncludedTraits)) },
		{ "filter-not-trait", ("Do not run any methods with a given trait value. Pass one or more name/value pairs (i.e., 'name=value'). Specifying more than one is an AND operation.", ArgumentArity.OneOrMore, (arguments, _, assemblyConfig) => OnFilterTrait(arguments, assemblyConfig.Filters.ExcludedTraits)) },

		// Reports
		{ "report-ctrf", ("Output results to CTRF file. Pass an output filename.", ArgumentArity.ExactlyOne, (arguments, projectConfig, _) => OnReport("ctrf", arguments[0], projectConfig)) },
		{ "report-html", ("Output results to HTML file. Pass an output filename.", ArgumentArity.ExactlyOne, (arguments, projectConfig, _) => OnReport("html", arguments[0], projectConfig)) },
		{ "report-junit", ("Output results to JUnit XML file. Pass an output filename.", ArgumentArity.ExactlyOne, (arguments, projectConfig, _) => OnReport("junit", arguments[0], projectConfig)) },
		{ "report-nunit", ("Output results to NUnit 2.5 XML file. Pass an output filename.", ArgumentArity.ExactlyOne, (arguments, projectConfig, _) => OnReport("nunit", arguments[0], projectConfig)) },
		{ "report-xml", ("Output results to xUnit.net v2+ XML file. Pass an output filename.", ArgumentArity.ExactlyOne, (arguments, projectConfig, _) => OnReport("xml", arguments[0], projectConfig)) },

		// Non-configuration options (read externally)
		{ "xunit-info", ("Show xUnit.net headers and information", ArgumentArity.Zero, (_1, _2, _3) => { }) },
	};

	public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
		options.Select(option => new CommandLineOption(option.Key, option.Value.Description, option.Value.Arity, isHidden: false)).ToArray();

	static void EnsurePathExists(string path)
	{
		var directory = Path.GetDirectoryName(path);

		if (string.IsNullOrEmpty(directory))
			return;

		Directory.CreateDirectory(directory);
	}

	static void OnCulture(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig)
	{
		var culture = arguments[0];

		assemblyConfig.Culture = culture.ToUpperInvariant() switch
		{
			"DEFAULT" => null,
			"INVARIANT" => string.Empty,
			_ => culture,
		};

		// Validate the provided culture; this isn't foolproof, since the system will accept random names, but it
		// will catch some simple cases like trying to pass a number as the culture
		if (!string.IsNullOrWhiteSpace(assemblyConfig.Culture))
		{
			try
			{
				CultureInfo.GetCultureInfo(culture);
			}
			catch (CultureNotFoundException)
			{
				throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Invalid value '{0}'", culture));
			}
		}
	}

	static void OnDiagnostics(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.DiagnosticMessages = ParseOnOff(arguments[0]);

	static void OnExplicit(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.ExplicitOption = ParseEnum<ExplicitOption>(arguments[0]);

	static void OnFailSkips(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.FailSkips = ParseOnOff(arguments[0]);

	static void OnFailWarns(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.FailTestsWithWarnings = ParseOnOff(arguments[0]);

	static void OnFilter(
		string[] arguments,
		ICollection<string> values) =>
			arguments.ForEach(values.Add);

	static void OnFilterTrait(
		string[] arguments,
		Dictionary<string, HashSet<string>> values) =>
			arguments.ForEach(argument =>
			{
				var pieces = argument.Split('=');
				if (pieces.Length != 2 || string.IsNullOrEmpty(pieces[0]) || string.IsNullOrEmpty(pieces[1]))
					throw new ArgumentException("Invalid trait format (must be \"name=value\")");

				values.Add(pieces[0], pieces[1]);
			});

	static void OnInternalDiagnostics(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.InternalDiagnosticMessages = ParseOnOff(arguments[0]);

	static void OnMaxThreads(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.MaxParallelThreads = arguments[0].ToUpperInvariant() switch
			{
				"0" => null,
				"DEFAULT" => null,
				"UNLIMITED" => -1,
				_ => ParseMaxThreadsValue(arguments[0]),
			};

	static void OnMethodDisplay(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.MethodDisplay = ParseEnum<TestMethodDisplay>(arguments[0]);

	static void OnMethodDisplayOptions(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig)
	{
		if (arguments.Any(a => a.Equals("all", StringComparison.OrdinalIgnoreCase)))
		{
			if (arguments.Length > 1)
				throw new ArgumentException("Cannot specify 'all' with any other values");

			assemblyConfig.MethodDisplayOptions = TestMethodDisplayOptions.All;
		}
		else if (arguments.Any(a => a.Equals("none", StringComparison.OrdinalIgnoreCase)))
		{
			if (arguments.Length > 1)
				throw new ArgumentException("Cannot specify 'none' with any other values");

			assemblyConfig.MethodDisplayOptions = TestMethodDisplayOptions.None;
		}
		else
		{
			assemblyConfig.MethodDisplayOptions = TestMethodDisplayOptions.None;

			foreach (var argument in arguments)
				assemblyConfig.MethodDisplayOptions |= ParseEnum<TestMethodDisplayOptions>(argument);
		}
	}

	static void OnParallel(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.ParallelizeTestCollections = arguments[0].ToUpperInvariant() switch
			{
				"NONE" => false,
				"COLLECTIONS" => true,
				_ => throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Invalid value '{0}' (must be one of 'none', 'collections')", arguments[0])),
			};

	static void OnParallelAlgorithm(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.ParallelAlgorithm = ParseEnum<ParallelAlgorithm>(arguments[0]);

	static void OnPreEnumerateTheories(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.PreEnumerateTheories = ParseOnOff(arguments[0]);

	static void OnSeed(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig)
	{
		if (!int.TryParse(arguments[0], NumberStyles.None, NumberFormatInfo.CurrentInfo, out int seed) || seed < 0)
			throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Invalid value '{0}' (must be an integer in the range of 0 - 2147483647)", arguments[0]));

		assemblyConfig.Seed = seed;
	}

	static void OnReport(
		string transform,
		string pathName,
		TestProjectConfiguration projectConfig)
	{
		EnsurePathExists(pathName);
		projectConfig.Output.Add(transform, pathName);
	}

	static void OnShowLiveOutput(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.ShowLiveOutput = ParseOnOff(arguments[0]);

	static void OnStopOnFail(
		string[] arguments,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig) =>
			assemblyConfig.StopOnFail = ParseOnOff(arguments[0]);

	public static void Parse(
		ICommandLineOptions commandLineOptions,
		TestProjectConfiguration projectConfig,
		TestAssemblyConfiguration assemblyConfig)
	{
		foreach (var option in options)
			if (commandLineOptions.TryGetOptionArgumentList(option.Key, out var arguments))
				option.Value.Parse(arguments, projectConfig, assemblyConfig);
	}

	static TEnum ParseEnum<TEnum>(string value)
		where TEnum : struct
	{
		if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
			return result;

		throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Invalid value '{0}' (must be one of {1})", value, string.Join(", ", Enum.GetValues(typeof(TEnum)).OfType<object>().Select(e => "'" + e + "'"))));
	}

	static int ParseMaxThreadsValue(string value)
	{
		var match = ConfigUtility.MultiplierStyleMaxParallelThreadsRegex.Match(value);

		// Use invariant format and convert ',' to '.' so we can always support both formats, regardless of locale
		// If we stick to locale-only parsing, we could break people when moving from one locale to another (for example,
		// from people running tests on their desktop in a comma locale vs. running them in CI with a decimal locale).
		if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var maxThreadMultiplier))
			return (int)(maxThreadMultiplier * Environment.ProcessorCount);
		if (int.TryParse(value, out var threadValue) && threadValue > 0)
			return threadValue;

		throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Invalid value '{0}' (must be one of 'default', 'unlimited', a positive number, a multiplier in the form of '{1}x')", value, 0.0m));
	}

	static bool ParseOnOff(string value) =>
		value.ToUpperInvariant() switch
		{
			"ON" => true,
			"OFF" => false,
			_ => throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Invalid value '{0}' (must be 'on', 'off')", value)),
		};

	public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions)
	{
		try
		{
			var projectConfig = new TestProjectConfiguration();
			var assemblyConfig = new TestAssemblyConfiguration();
			Parse(commandLineOptions, projectConfig, assemblyConfig);
			return ValidationResult.ValidTask;
		}
		catch (ArgumentException argEx)
		{
			return ValidationResult.InvalidTask(argEx.Message);
		}
	}

	public Task<ValidationResult> ValidateOptionArgumentsAsync(
		CommandLineOption commandOption,
		string[] arguments)
	{
		if (options.TryGetValue(commandOption.Name, out var option))
		{
			try
			{
				var projectConfig = new TestProjectConfiguration();
				var assemblyConfig = new TestAssemblyConfiguration();
				option.Parse(arguments, projectConfig, assemblyConfig);
			}
			catch (ArgumentException argEx)
			{
				return ValidationResult.InvalidTask(argEx.Message);
			}
		}

		return ValidationResult.ValidTask;
	}
}

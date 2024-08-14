using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xunit;
using Xunit.Runner.Common;
using Xunit.Runner.InProc.SystemConsole;

public class CommandLineTests
{
	public class UnknownOption
	{
		[Fact]
		public static void UnknownOptionThrows()
		{
			var commandLine = new TestableCommandLine("-unknown");

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal("unknown option: -unknown", exception.Message);
		}
	}

	public class Project
	{
		[Fact]
		public static void DefaultValues()
		{
			var commandLine = new TestableCommandLine();

			var assembly = commandLine.Parse();

			Assert.Equal($"/full/path/{typeof(CommandLineTests).Assembly.Location}", assembly.AssemblyFileName);
			Assert.Null(assembly.ConfigFileName);
		}

		[Fact]
		public static void ConfigFileDoesNotExist_Throws()
		{
			var commandLine = new TestableCommandLine("badConfig.json");

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal("config file not found: badConfig.json", exception.Message);
		}

		[Fact]
		public static void ConfigFileUnsupportedFormat_Throws()
		{
			var commandLine = new TestableCommandLine("assembly1.config");

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal("unknown option: assembly1.config", exception.Message);
		}

		[Fact]
		public static void TwoConfigFiles_Throws()
		{
			var commandLine = new TestableCommandLine("assembly1.json", "assembly2.json");

			var exception = Record.Exception(() => commandLine.Parse());

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal("unknown option: assembly2.json", exception.Message);
		}

		[Fact]
		public static void WithConfigFile()
		{
			var commandLine = new TestableCommandLine("assembly1.json");

			var assembly = commandLine.Parse();

			Assert.Equal("/full/path/assembly1.json", assembly.ConfigFileName);
		}
	}

	[Collection("Switches Test Collection")]
	public sealed class Switches : IDisposable
	{
		readonly string? _originalNoColorValue;

		public Switches()
		{
			_originalNoColorValue = Environment.GetEnvironmentVariable(TestProjectConfiguration.EnvNameNoColor);
			Environment.SetEnvironmentVariable(TestProjectConfiguration.EnvNameNoColor, null);
		}

		public void Dispose() =>
			Environment.SetEnvironmentVariable(TestProjectConfiguration.EnvNameNoColor, _originalNoColorValue);

		static readonly (string Switch, Expression<Func<XunitProjectAssembly, bool>> Accessor)[] SwitchOptionsList =
		[
			("-debug", assembly => assembly.Project.Configuration.DebugOrDefault),
			("-diagnostics", assembly => assembly.Configuration.DiagnosticMessagesOrDefault),
			("-failskips", assembly => assembly.Configuration.FailSkipsOrDefault),
			("-ignorefailures", assembly => assembly.Project.Configuration.IgnoreFailuresOrDefault),
			("-internaldiagnostics", assembly => assembly.Configuration.InternalDiagnosticMessagesOrDefault),
			("-noautoreporters", assembly => assembly.Project.Configuration.NoAutoReportersOrDefault),
			("-nocolor", assembly => assembly.Project.Configuration.NoColorOrDefault),
			("-nologo", assembly => assembly.Project.Configuration.NoLogoOrDefault),
			("-pause", assembly => assembly.Project.Configuration.PauseOrDefault),
			("-preenumeratetheories", assembly => assembly.Configuration.PreEnumerateTheories ?? false),
			("-showliveoutput", assembly => assembly.Configuration.ShowLiveOutputOrDefault),
			("-stoponfail", assembly => assembly.Configuration.StopOnFailOrDefault),
			("-wait", assembly => assembly.Project.Configuration.WaitOrDefault),
		];

		public static readonly TheoryData<string, Expression<Func<XunitProjectAssembly, bool>>> SwitchesLowerCase =
			new(SwitchOptionsList);

		public static readonly TheoryData<string, Expression<Func<XunitProjectAssembly, bool>>> SwitchesUpperCase =
			new(SwitchOptionsList.Select(t => (t.Switch.ToUpperInvariant(), t.Accessor)));

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public void SwitchDefault(
			string _,
			Expression<Func<XunitProjectAssembly, bool>> accessor)
		{
			var commandLine = new TestableCommandLine("no-config.json");
			var assembly = commandLine.Parse();

			var result = accessor.Compile().Invoke(assembly);

			Assert.False(result);
		}

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public void SwitchOverride(
			string @switch,
			Expression<Func<XunitProjectAssembly, bool>> accessor)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch);
			var assembly = commandLine.Parse();

			var result = accessor.Compile().Invoke(assembly);

			Assert.True(result);
		}

		[Fact]
		public void NoColorSetsEnvironmentVariable()
		{
			Assert.Null(Environment.GetEnvironmentVariable(TestProjectConfiguration.EnvNameNoColor));

			new TestableCommandLine("no-config.json", "-nocolor").Parse();

			// Any set (non-null, non-empty) value is acceptable, see https://no-color.org/
			var envValue = Environment.GetEnvironmentVariable(TestProjectConfiguration.EnvNameNoColor);
			Assert.NotNull(envValue);
			Assert.NotEmpty(envValue);
		}
	}

	public class OptionsWithArguments
	{
		public class Automated
		{
			[Fact]
			public static void DefaultValueIsNull()
			{
				var commandLine = new TestableCommandLine("no-config.json");

				var assembly = commandLine.Parse();

				Assert.Null(assembly.Configuration.SynchronousMessageReporting);
			}

			[Fact]
			public static void UnspecifiedValueIsNull()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-automated");

				var assembly = commandLine.Parse();

				Assert.Null(assembly.Configuration.SynchronousMessageReporting);
			}

			[Fact]
			public static void AsyncIsFalse()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-automated", "async");

				var assembly = commandLine.Parse();

				Assert.False(assembly.Configuration.SynchronousMessageReporting);
			}

			[Fact]
			public static void SyncIsTrue()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-automated", "sync");

				var assembly = commandLine.Parse();

				Assert.True(assembly.Configuration.SynchronousMessageReporting);
			}
		}

		public class Culture
		{
			[Fact]
			public static void DefaultValueIsNull()
			{
				var commandLine = new TestableCommandLine("no-config.json");

				var assembly = commandLine.Parse();

				Assert.Null(assembly.Configuration.Culture);
			}

			[Fact]
			public static void ExplicitDefaultValueIsNull()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-culture", "default");

				var assembly = commandLine.Parse();

				Assert.Null(assembly.Configuration.Culture);
			}

			[Fact]
			public static void InvariantCultureIsEmptyString()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-culture", "invariant");

				var assembly = commandLine.Parse();

				Assert.Equal(string.Empty, assembly.Configuration.Culture);
			}

			[Fact]
			public static void ValueIsPreserved()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-culture", "foo");

				var assembly = commandLine.Parse();

				Assert.Equal("foo", assembly.Configuration.Culture);
			}
		}

		public class MaxThreads
		{
			[Fact]
			public static void DefaultValueIsNull()
			{
				var commandLine = new TestableCommandLine("no-config.json");

				var assembly = commandLine.Parse();

				Assert.Null(assembly.Configuration.MaxParallelThreads);
			}

			[Fact]
			public static void MissingValue()
			{
				var commandLine = new TestableCommandLine("no-config.json", "-maxthreads");

				var exception = Record.Exception(() => commandLine.Parse());

				Assert.IsType<ArgumentException>(exception);
				Assert.Equal("missing argument for -maxthreads", exception.Message);
			}

			[Theory]
			[InlineData("abc")]
			// Non digit
			[InlineData("0.ax")]
			[InlineData("0,ax")]
			// Missing leading digit
			[InlineData(".0x")]
			[InlineData(",0x")]
			public static void InvalidValues(string value)
			{
				var commandLine = new TestableCommandLine("no-config.json", "-maxthreads", value);

				var exception = Record.Exception(commandLine.Parse);

				Assert.IsType<ArgumentException>(exception);
				Assert.Equal($"incorrect argument value for -maxthreads (must be 'default', 'unlimited', a positive number, or a multiplier in the form of '{0.0m}x')", exception.Message);
			}

			[Theory]
			[InlineData("default", null)]
			[InlineData("0", null)]
			[InlineData("unlimited", -1)]
			[InlineData("16", 16)]
			public static void ValidValues(
				string value,
				int? expected)
			{
				var commandLine = new TestableCommandLine("no-config.json", "-maxthreads", value);

				var assembly = commandLine.Parse();

				Assert.Equal(expected, assembly.Configuration.MaxParallelThreads);
			}

			[Theory]
			[InlineData("2x")]
			[InlineData("2.0x")]
			[InlineData("2,0x")]
			public static void MultiplierValue(string value)
			{
				var expected = Environment.ProcessorCount * 2;
				var commandLine = new TestableCommandLine("no-config.json", "-maxthreads", value);

				var assembly = commandLine.Parse();

				Assert.Equal(expected, assembly.Configuration.MaxParallelThreads);
			}
		}

		public class Parallelization
		{
			[Fact]
			public static void ParallelizationOptionsAreNullByDefault()
			{
				var commandLine = new TestableCommandLine("no-config.json");

				var assembly = commandLine.Parse();

				Assert.Null(assembly.Configuration.ParallelizeTestCollections);
			}

			[Fact]
			public static void FailsWithoutOptionOrWithIncorrectOptions()
			{
				var commandLine1 = new TestableCommandLine("no-config.json", "-parallel");
				var exception1 = Record.Exception(commandLine1.Parse);
				Assert.IsType<ArgumentException>(exception1);
				Assert.Equal("missing argument for -parallel", exception1.Message);

				var commandLine2 = new TestableCommandLine("no-config.json", "-parallel", "nonsense");
				var exception2 = Record.Exception(commandLine2.Parse);
				Assert.IsType<ArgumentException>(exception2);
				Assert.Equal("incorrect argument value for -parallel", exception2.Message);
			}

			[Theory]
			[InlineData("none", false)]
			[InlineData("collections", true)]
			public static void ParallelCanBeTurnedOn(
				string parallelOption,
				bool expectedCollectionsParallelization)
			{
				var commandLine = new TestableCommandLine("no-config.json", "-parallel", parallelOption);

				var assembly = commandLine.Parse();

				Assert.Equal(expectedCollectionsParallelization, assembly.Configuration.ParallelizeTestCollections);
			}
		}
	}

	public class Filters
	{
		[Fact]
		public static void DefaultFilters()
		{
			var commandLine = new TestableCommandLine("no-config.json");

			var assembly = commandLine.Parse();

			var filters = assembly.Configuration.Filters;
			Assert.Empty(filters.IncludedTraits);
			Assert.Empty(filters.ExcludedTraits);
			Assert.Empty(filters.IncludedNamespaces);
			Assert.Empty(filters.ExcludedNamespaces);
			Assert.Empty(filters.IncludedClasses);
			Assert.Empty(filters.ExcludedClasses);
			Assert.Empty(filters.IncludedMethods);
			Assert.Empty(filters.ExcludedMethods);
		}

		static readonly (string Switch, Expression<Func<XunitProjectAssembly, ICollection<string>>> Accessor)[] SwitchOptionsList =
		[
			("-namespace", assembly => assembly.Configuration.Filters.IncludedNamespaces),
			("-nonamespace", assembly => assembly.Configuration.Filters.ExcludedNamespaces),
			("-class", assembly => assembly.Configuration.Filters.IncludedClasses),
			("-noclass", assembly => assembly.Configuration.Filters.ExcludedClasses),
			("-method", assembly => assembly.Configuration.Filters.IncludedMethods),
			("-nomethod", assembly => assembly.Configuration.Filters.ExcludedMethods),
		];

		public static readonly TheoryData<string, Expression<Func<XunitProjectAssembly, ICollection<string>>>> SwitchesLowerCase =
			new(SwitchOptionsList);

		public static readonly TheoryData<string, Expression<Func<XunitProjectAssembly, ICollection<string>>>> SwitchesUpperCase =
			new(SwitchOptionsList.Select(t => (t.Switch.ToUpperInvariant(), t.Accessor)));

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void MissingOptionValue(
			string @switch,
			Expression<Func<XunitProjectAssembly, ICollection<string>>> _)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch);

			var exception = Record.Exception(commandLine.Parse);

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal($"missing argument for {@switch.ToLowerInvariant()}", exception.Message);
		}

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void SingleValidArgument(
			string @switch,
			Expression<Func<XunitProjectAssembly, ICollection<string>>> accessor)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch, "value1");
			var assembly = commandLine.Parse();

			var results = accessor.Compile().Invoke(assembly);

			var item = Assert.Single(results.OrderBy(x => x));
			Assert.Equal("value1", item);
		}

		[Theory(DisableDiscoveryEnumeration = true)]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void MultipleValidArguments(
			string @switch,
			Expression<Func<XunitProjectAssembly, ICollection<string>>> accessor)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch, "value2", @switch, "value1");
			var assembly = commandLine.Parse();

			var results = accessor.Compile().Invoke(assembly);

			Assert.Collection(results.OrderBy(x => x),
				item => Assert.Equal("value1", item),
				item => Assert.Equal("value2", item)
			);
		}

		public class Traits
		{
			static readonly (string Switch, Expression<Func<XunitProjectAssembly, Dictionary<string, HashSet<string>>>> Accessor)[] SwitchOptionsList =
			[
				("-trait", assembly => assembly.Configuration.Filters.IncludedTraits),
				("-notrait", assembly => assembly.Configuration.Filters.ExcludedTraits),
			];

			static readonly string[] BadFormatValues =
			[
				// Missing equals
				"foobar",
				// Missing value
				"foo=",
				// Missing name
				"=bar",
				// Double equal signs
				"foo=bar=baz",
			];

			public static readonly TheoryData<string, Expression<Func<XunitProjectAssembly, Dictionary<string, HashSet<string>>>>> SwitchesLowerCase =
				new(SwitchOptionsList);

			public static readonly TheoryData<string, Expression<Func<XunitProjectAssembly, Dictionary<string, HashSet<string>>>>> SwitchesUpperCase =
				new(SwitchOptionsList.Select(x => (x.Switch.ToUpperInvariant(), x.Accessor)));

			public static readonly TheoryData<string, string> SwitchesWithOptionsLowerCase =
				new(SwitchOptionsList.SelectMany(tuple => BadFormatValues.Select(value => (tuple.Switch, value))));

			public static readonly TheoryData<string, string> SwitchesWithOptionsUpperCase =
				new(SwitchOptionsList.SelectMany(tuple => BadFormatValues.Select(value => (tuple.Switch.ToUpperInvariant(), value))));

			[Theory(DisableDiscoveryEnumeration = true)]
			[MemberData(nameof(SwitchesLowerCase))]
			[MemberData(nameof(SwitchesUpperCase))]
			public static void SingleValidTraitArgument(
				string @switch,
				Expression<Func<XunitProjectAssembly, Dictionary<string, HashSet<string>>>> accessor)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch, "foo=bar");
				var assembly = commandLine.Parse();

				var traits = accessor.Compile().Invoke(assembly);

				Assert.Single(traits);
				Assert.Single(traits["foo"]);
				Assert.Contains("bar", traits["foo"]);
			}

			[Theory(DisableDiscoveryEnumeration = true)]
			[MemberData(nameof(SwitchesLowerCase))]
			[MemberData(nameof(SwitchesUpperCase))]
			public static void MultipleValidTraitArguments_SameName(
				string @switch,
				Expression<Func<XunitProjectAssembly, Dictionary<string, HashSet<string>>>> accessor)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch, "foo=bar", @switch, "foo=baz");
				var assembly = commandLine.Parse();

				var traits = accessor.Compile().Invoke(assembly);

				Assert.Single(traits);
				Assert.Equal(2, traits["foo"].Count);
				Assert.Contains("bar", traits["foo"]);
				Assert.Contains("baz", traits["foo"]);
			}

			[Theory(DisableDiscoveryEnumeration = true)]
			[MemberData(nameof(SwitchesLowerCase))]
			[MemberData(nameof(SwitchesUpperCase))]
			public static void MultipleValidTraitArguments_DifferentName(
				string @switch,
				Expression<Func<XunitProjectAssembly, Dictionary<string, HashSet<string>>>> accessor)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch, "foo=bar", @switch, "baz=biff");
				var assembly = commandLine.Parse();

				var traits = accessor.Compile().Invoke(assembly);

				Assert.Equal(2, traits.Count);
				Assert.Single(traits["foo"]);
				Assert.Contains("bar", traits["foo"]);
				Assert.Single(traits["baz"]);
				Assert.Contains("biff", traits["baz"]);
			}

			[Theory(DisableDiscoveryEnumeration = true)]
			[MemberData(nameof(SwitchesLowerCase))]
			[MemberData(nameof(SwitchesUpperCase))]
			public static void MissingOptionValue(
				string @switch,
				Expression<Func<XunitProjectAssembly, Dictionary<string, HashSet<string>>>> _)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch);

				var exception = Record.Exception(commandLine.Parse);

				Assert.IsType<ArgumentException>(exception);
				Assert.Equal($"missing argument for {@switch.ToLowerInvariant()}", exception.Message);
			}

			[Theory]
			[MemberData(nameof(SwitchesWithOptionsLowerCase))]
			[MemberData(nameof(SwitchesWithOptionsUpperCase))]
			public static void ImproperlyFormattedOptionValue(
				string @switch,
				string optionValue)
			{
				var commandLine = new TestableCommandLine("no-config.json", @switch, optionValue);

				var exception = Record.Exception(commandLine.Parse);

				Assert.IsType<ArgumentException>(exception);
				Assert.Equal($"incorrect argument format for {@switch.ToLowerInvariant()} (should be \"name=value\")", exception.Message);
			}
		}
	}

	public class Transforms
	{
		public static readonly TheoryData<string> SwitchesLowerCase =
			new(TransformFactory.AvailableTransforms.Select(x => $"-{x.ID}"));

		public static readonly TheoryData<string> SwitchesUpperCase =
			new(TransformFactory.AvailableTransforms.Select(x => $"-{x.ID.ToUpperInvariant()}"));

		[Theory]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void OutputMissingFilename(string @switch)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch);

			var exception = Record.Exception(commandLine.Parse);

			Assert.IsType<ArgumentException>(exception);
			Assert.Equal($"missing filename for {@switch}", exception.Message);
		}

		[Theory]
		[MemberData(nameof(SwitchesLowerCase))]
		[MemberData(nameof(SwitchesUpperCase))]
		public static void Output(string @switch)
		{
			var commandLine = new TestableCommandLine("no-config.json", @switch, "outputFile");

			var assembly = commandLine.Parse();

			var output = Assert.Single(assembly.Project.Configuration.Output);
			Assert.Equal(@switch.Substring(1), output.Key, ignoreCase: true);
			Assert.Equal("outputFile", output.Value);
		}
	}

	public sealed class Reporters : IDisposable
	{
		readonly IDisposable environmentCleanup;

		public Reporters() =>
			environmentCleanup = EnvironmentHelper.NullifyEnvironmentalReporters();

		public void Dispose() =>
			environmentCleanup.Dispose();

		[Fact]
		public void NoReporters_UsesDefaultReporter()
		{
			var commandLine = new TestableCommandLine("no-config.json");

			var assembly = commandLine.Parse();

			Assert.IsType<DefaultRunnerReporter>(assembly.Project.RunnerReporter);
		}

		[Fact]
		public void NoExplicitReporter_NoEnvironmentallyEnabledReporters_UsesDefaultReporter()
		{
			var implicitReporter = Mocks.RunnerReporter(isEnvironmentallyEnabled: false);
			var commandLine = new TestableCommandLine([implicitReporter], "no-config.json");

			var assembly = commandLine.Parse();

			Assert.IsType<DefaultRunnerReporter>(assembly.Project.RunnerReporter);
		}

		[Fact]
		public void ExplicitReporter_NoEnvironmentalOverride_UsesExplicitReporter()
		{
			var explicitReporter = Mocks.RunnerReporter("switch");
			var commandLine = new TestableCommandLine([explicitReporter], "no-config.json", "-switch");

			var assembly = commandLine.Parse();

			Assert.Same(explicitReporter, assembly.Project.RunnerReporter);
		}

		[Fact]
		public void ExplicitReporter_WithEnvironmentalOverride_UsesEnvironmentalOverride()
		{
			var explicitReporter = Mocks.RunnerReporter("switch");
			var implicitReporter = Mocks.RunnerReporter(isEnvironmentallyEnabled: true);
			var commandLine = new TestableCommandLine([explicitReporter, implicitReporter], "no-config.json", "-switch");

			var assembly = commandLine.Parse();

			Assert.Same(implicitReporter, assembly.Project.RunnerReporter);
		}

		[Fact]
		public void WithEnvironmentalOverride_WithEnvironmentalOverridesDisabled_UsesDefaultReporter()
		{
			var implicitReporter = Mocks.RunnerReporter(isEnvironmentallyEnabled: true);
			var commandLine = new TestableCommandLine([implicitReporter], "no-config.json", "-noautoreporters");

			var assembly = commandLine.Parse();

			Assert.IsType<DefaultRunnerReporter>(assembly.Project.RunnerReporter);
		}

		[Fact]
		public void NoExplicitReporter_SelectsFirstEnvironmentallyEnabledReporter()
		{
			var explicitReporter = Mocks.RunnerReporter("switch");
			var implicitReporter1 = Mocks.RunnerReporter(isEnvironmentallyEnabled: true);
			var implicitReporter2 = Mocks.RunnerReporter(isEnvironmentallyEnabled: true);
			var commandLine = new TestableCommandLine([explicitReporter, implicitReporter1, implicitReporter2], "no-config.json");

			var assembly = commandLine.Parse();

			Assert.Same(implicitReporter1, assembly.Project.RunnerReporter);
		}
	}

	class TestableCommandLine : CommandLine
	{
		public TestableCommandLine(params string[] arguments)
			: base(new ConsoleHelper(TextReader.Null, TextWriter.Null), Assembly.GetExecutingAssembly(), arguments)
		{ }

		public TestableCommandLine(
			IReadOnlyList<IRunnerReporter> reporters,
			params string[] arguments)
				: base(new ConsoleHelper(TextReader.Null, TextWriter.Null), Assembly.GetExecutingAssembly(), arguments, reporters)
		{ }

		protected override bool FileExists(string? path) =>
			path?.StartsWith("badConfig.") != true && path != "fileName";

		protected override string? GetFullPath(string? fileName) =>
			fileName is null ? null : $"/full/path/{fileName}";
	}
}

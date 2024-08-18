using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit.Internal;
using Xunit.Runner.Common;
using Xunit.Sdk;
using Xunit.v3;
using DiagnosticMessage = Xunit.Runner.Common.DiagnosticMessage;
using ErrorMessage = Xunit.Runner.Common.ErrorMessage;

namespace Xunit.Runner.InProc.SystemConsole;

/// <summary>
/// The project assembly runner class, used by <see cref="ConsoleRunner"/>.
/// </summary>
/// <param name="testAssembly">The assembly under test</param>
/// <param name="cancelThunk">The thunk to determine if we should cancel</param>
/// <param name="automatedMode">The automated mode we're running in</param>
public sealed class ProjectAssemblyRunner(
	Assembly testAssembly,
	Func<bool> cancelThunk,
	AutomatedMode automatedMode)
{
	readonly AutomatedMode automatedMode = automatedMode;
	volatile bool cancel;
	readonly Func<bool> cancelThunk = cancelThunk;
	bool failed;
	readonly Assembly testAssembly = testAssembly;

	/// <summary>
	/// Gets a one-line banner to be printed when the runner is executed.
	/// </summary>
	public static string Banner =>
		string.Format(
			CultureInfo.CurrentCulture,
			"xUnit.net v3 In-Process Runner v{0} ({1}-bit {2})",
			ThisAssembly.AssemblyInformationalVersion,
			IntPtr.Size * 8,
			RuntimeInformation.FrameworkDescription
		);

	/// <summary>
	/// Gets the summaries of the test execution, once it is finished.
	/// </summary>
	public TestExecutionSummaries TestExecutionSummaries { get; } = new();

	/// <summary>
	/// Discovers tests in the given test project.
	/// </summary>
	/// <param name="assembly">The test project assembly</param>
	/// <param name="pipelineStartup">The pipeline startup object</param>
	/// <param name="messageSink">The optional message sink to send messages to</param>
	/// <param name="diagnosticMessageSink">The optional message sink to send diagnostic messages to</param>
	/// <param name="testCases">A collection to contain the test cases to run, if desired</param>
	public async ValueTask Discover(
		XunitProjectAssembly assembly,
		ITestPipelineStartup? pipelineStartup,
		IMessageSink? messageSink = null,
		IMessageSink? diagnosticMessageSink = null,
		IList<(ITestCase TestCase, bool PassedFilter)>? testCases = null)
	{
		Guard.ArgumentNotNull(assembly);

		// Default to false for console runners
		assembly.Configuration.PreEnumerateTheories ??= false;

		// Setup discovery options with command-line overrides
		var discoveryOptions = TestFrameworkOptions.ForDiscovery(assembly.Configuration);
		var diagnosticMessages = assembly.Configuration.DiagnosticMessagesOrDefault;
		var internalDiagnosticMessages = assembly.Configuration.InternalDiagnosticMessagesOrDefault;

		TestContext.SetForInitialization(diagnosticMessageSink, diagnosticMessages, internalDiagnosticMessages);

		await using var disposalTracker = new DisposalTracker();
		var testFramework = ExtensibilityPointFactory.GetTestFramework(testAssembly);
		disposalTracker.Add(testFramework);

		if (pipelineStartup is not null)
			testFramework.SetTestPipelineStartup(pipelineStartup);

		var types =
			assembly.Configuration.Filters.IncludedClasses.Count == 0 || assembly.Assembly is null
				? null
				: assembly.Configuration.Filters.IncludedClasses.Select(assembly.Assembly.GetType).WhereNotNull().ToArray();

		var frontController = new InProcessFrontController(testFramework, testAssembly, assembly.ConfigFileName);

		await frontController.Find(
			messageSink,
			discoveryOptions,
			assembly.Configuration.Filters.Filter,
			types,
			(testCase, passedFilter) =>
			{
				testCases?.Add((testCase, passedFilter));

				if (passedFilter && messageSink?.OnMessage(testCase.ToTestCaseDiscovered()) == false)
					return new(false);

				return new(!cancelThunk());
			}
		);
	}

	/// <summary>
	/// Invoke the instance of <see cref="ITestPipelineStartup"/>, if it exists, and returns the instance
	/// that was created.
	/// </summary>
	/// <param name="testAssembly">The test assembly under test</param>
	/// <param name="diagnosticMessageSink">The optional diagnostic message sink to report diagnostic messages to</param>
	public static async ValueTask<ITestPipelineStartup?> InvokePipelineStartup(
		Assembly testAssembly,
		IMessageSink? diagnosticMessageSink)
	{
		Guard.ArgumentNotNull(testAssembly);

		var result = default(ITestPipelineStartup);

		var pipelineStartupAttributes = testAssembly.GetMatchingCustomAttributes(typeof(ITestPipelineStartupAttribute));
		if (pipelineStartupAttributes.Count > 1)
			throw new TestPipelineException("More than one pipeline startup attribute was specified: " + pipelineStartupAttributes.Select(a => a.GetType()).ToCommaSeparatedList());

		if (pipelineStartupAttributes.FirstOrDefault() is ITestPipelineStartupAttribute pipelineStartupAttribute)
		{
			var pipelineStartupType = pipelineStartupAttribute.TestPipelineStartupType;
			if (!typeof(ITestPipelineStartup).IsAssignableFrom(pipelineStartupType))
				throw new TestPipelineException(string.Format(CultureInfo.CurrentCulture, "Pipeline startup type '{0}' does not implement '{1}'", pipelineStartupType.SafeName(), typeof(ITestPipelineStartup).SafeName()));

			try
			{
				result = Activator.CreateInstance(pipelineStartupType) as ITestPipelineStartup;
			}
			catch (Exception ex)
			{
				throw new TestPipelineException(string.Format(CultureInfo.CurrentCulture, "Pipeline startup type '{0}' threw during construction", pipelineStartupType.SafeName()), ex);
			}

			if (result is null)
				throw new TestPipelineException(string.Format(CultureInfo.CurrentCulture, "Pipeline startup type '{0}' does not implement '{1}'", pipelineStartupType.SafeName(), typeof(ITestPipelineStartup).SafeName()));

			await result.StartAsync(diagnosticMessageSink ?? NullMessageSink.Instance);
		}

		return result;
	}

	/// <summary>
	/// Prints the program header.
	/// </summary>
	/// <param name="consoleHelper">The console helper to use for output</param>
	public static void PrintHeader(ConsoleHelper consoleHelper) =>
		Guard.ArgumentNotNull(consoleHelper).WriteLine(
			"xUnit.net v3 In-Process Runner v{0} ({1}-bit {2})",
			ThisAssembly.AssemblyInformationalVersion,
			IntPtr.Size * 8,
			RuntimeInformation.FrameworkDescription
		);

	/// <summary>
	/// Runs the given test project.
	/// </summary>
	/// <param name="assembly">The test project assembly</param>
	/// <param name="messageSink">The message sink to send messages to</param>
	/// <param name="diagnosticMessageSink">The optional message sink to send diagnostic messages to</param>
	/// <param name="runnerLogger">The runner logger, to log console output to</param>
	/// <param name="pipelineStartup">The pipeline startup object</param>
	/// <param name="testCaseIDsToRun">An optional list of test case unique IDs to run</param>
	/// <returns>Returns <c>0</c> if there were no failures; non-<c>zero</c> failure count, otherwise</returns>
	public async ValueTask<int> Run(
		XunitProjectAssembly assembly,
		IMessageSink messageSink,
		IMessageSink? diagnosticMessageSink,
		IRunnerLogger runnerLogger,
		ITestPipelineStartup? pipelineStartup,
		HashSet<string>? testCaseIDsToRun = null)
	{
		Guard.ArgumentNotNull(assembly);
		Guard.ArgumentNotNull(messageSink);
		Guard.ArgumentNotNull(runnerLogger);

		XElement? assemblyElement = null;
		var clockTime = Stopwatch.StartNew();
		var xmlTransformers = TransformFactory.GetXmlTransformers(assembly.Project);
		var needsXml = xmlTransformers.Count > 0;

		if (needsXml)
			assemblyElement = new XElement("assembly");

		var originalWorkingFolder = Directory.GetCurrentDirectory();

		try
		{
			// Default to false for console runners
			assembly.Configuration.PreEnumerateTheories ??= false;

			// Setup discovery and execution options with command-line overrides
			var discoveryOptions = TestFrameworkOptions.ForDiscovery(assembly.Configuration);
			var executionOptions = TestFrameworkOptions.ForExecution(assembly.Configuration);

			var diagnosticMessages = assembly.Configuration.DiagnosticMessagesOrDefault;
			var internalDiagnosticMessages = assembly.Configuration.InternalDiagnosticMessagesOrDefault;
			var longRunningSeconds = assembly.Configuration.LongRunningTestSecondsOrDefault;

			TestContext.SetForInitialization(diagnosticMessageSink, diagnosticMessages, internalDiagnosticMessages);

			await using var disposalTracker = new DisposalTracker();
			var testFramework = ExtensibilityPointFactory.GetTestFramework(testAssembly);
			disposalTracker.Add(testFramework);

			if (pipelineStartup is not null)
				testFramework.SetTestPipelineStartup(pipelineStartup);

			var frontController = new InProcessFrontController(testFramework, testAssembly, assembly.ConfigFileName);

			var sinkOptions = new ExecutionSinkOptions
			{
				AssemblyElement = assemblyElement,
				CancelThunk = () => cancel || cancelThunk(),
				DiagnosticMessageSink = diagnosticMessageSink,
				FailSkips = assembly.Configuration.FailSkipsOrDefault,
				FailWarn = assembly.Configuration.FailTestsWithWarningsOrDefault,
				LongRunningTestTime = TimeSpan.FromSeconds(longRunningSeconds),
			};

			using var resultsSink = new ExecutionSink(assembly, discoveryOptions, executionOptions, AppDomainOption.NotAvailable, shadowCopy: false, messageSink, sinkOptions);
			var testCases =
				assembly
					.TestCasesToRun
					.Select(s => SerializationHelper.Deserialize(s) as ITestCase)
					.WhereNotNull()
					.ToArray();

			if (testCases.Length != 0)
				await frontController.Run(resultsSink, executionOptions, testCases);
			else
			{
				Func<ITestCaseMetadata, bool> filter = testCaseIDsToRun switch
				{
					not null => metadata => testCaseIDsToRun.Contains(metadata.UniqueID) && assembly.Configuration.Filters.Filter(metadata),
					_ => assembly.Configuration.Filters.Filter,
				};
				await frontController.FindAndRun(resultsSink, discoveryOptions, executionOptions, filter);
			}

			TestExecutionSummaries.Add(frontController.TestAssemblyUniqueID, resultsSink.ExecutionSummary);

			if (resultsSink.ExecutionSummary.Failed != 0 && executionOptions.GetStopOnTestFailOrDefault())
			{
				if (automatedMode != AutomatedMode.Off)
					runnerLogger.WriteMessage(new DiagnosticMessage("Cancelling due to test failure"));
				else
					runnerLogger.LogRaw("Cancelling due to test failure...");

				cancel = true;
			}
		}
		catch (Exception ex)
		{
			failed = true;

			var e = ex;
			while (e is not null)
			{
				if (automatedMode != AutomatedMode.Off)
					runnerLogger.WriteMessage(ErrorMessage.FromException(e));
				else
				{
					runnerLogger.LogRaw("{0}: {1}", e.GetType().SafeName(), e.Message);

					if (assembly.Configuration.InternalDiagnosticMessagesOrDefault && e.StackTrace is not null)
						runnerLogger.LogRaw(e.StackTrace);
				}

				e = e.InnerException;
			}
		}

		clockTime.Stop();

		TestExecutionSummaries.ElapsedClockTime = clockTime.Elapsed;
		messageSink.OnMessage(TestExecutionSummaries);

		Directory.SetCurrentDirectory(originalWorkingFolder);

		if (assemblyElement is not null)
		{
			var assembliesElement = TransformFactory.CreateAssembliesElement();
			assembliesElement.Add(assemblyElement);
			TransformFactory.FinishAssembliesElement(assembliesElement);

			xmlTransformers.ForEach(transformer => transformer(assembliesElement));
		}

		return failed ? 1 : TestExecutionSummaries.SummariesByAssemblyUniqueID.Sum(s => s.Summary.Failed + s.Summary.Errors);
	}
}

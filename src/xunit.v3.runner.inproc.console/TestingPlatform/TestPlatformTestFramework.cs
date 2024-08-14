using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;
using Xunit.Internal;
using Xunit.Runner.Common;
using Xunit.Sdk;
using Xunit.v3;
using VSTest_ITestFramework = Microsoft.Testing.Platform.Extensions.TestFramework.ITestFramework;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

/// <summary>
/// Implementation of <see cref="VSTest_ITestFramework"/> to run xUnit.net v3 test projects.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TestPlatformTestFramework :
	VSTest_ITestFramework
{
	readonly ConsoleHelper consoleHelper;
	readonly IMessageSink innerSink;
	readonly XunitProjectAssembly projectAssembly;
	readonly ConcurrentDictionary<SessionUid, CountdownEvent> operationCounterBySessionUid = new();
	readonly Assembly testAssembly;

	TestPlatformTestFramework(
		IMessageSink innerSink,
		XunitProjectAssembly projectAssembly,
		Assembly testAssembly,
		ConsoleHelper consoleHelper)
	{
		this.innerSink = innerSink;
		this.projectAssembly = projectAssembly;
		this.testAssembly = testAssembly;
		this.consoleHelper = consoleHelper;
	}

	/// <inheritdoc/>
	public string Description =>
		"xUnit.net v3 Microsoft.Testing.Platform test framework";

	/// <inheritdoc/>
	public string DisplayName =>
		Description;

	/// <inheritdoc/>
	public string Uid =>
		"30ea7c6e-dd24-4152-a360-1387158cd41d";

	/// <inheritdoc/>
	public string Version =>
		ThisAssembly.AssemblyVersion;

	/// <inheritdoc/>
	public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
	{
		Guard.ArgumentNotNull(context);

		if (!operationCounterBySessionUid.TryRemove(context.SessionUid, out var operationCounter))
			return Task.FromResult(new CloseTestSessionResult { IsSuccess = false, ErrorMessage = string.Format(CultureInfo.CurrentCulture, "Attempt to close unknown session UID {0}", context.SessionUid.Value) });

		operationCounter.Signal();
		operationCounter.Wait();
		operationCounter.Dispose();

		return Task.FromResult(new CloseTestSessionResult { IsSuccess = true });
	}

	/// <inheritdoc/>
	public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context)
	{
		if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XUNIT_TESTINGPLATFORM_DEBUG")))
			Debugger.Launch();

		Guard.ArgumentNotNull(context);

		if (!operationCounterBySessionUid.TryAdd(context.SessionUid, new CountdownEvent(1)))
			return Task.FromResult(new CreateTestSessionResult { IsSuccess = false, ErrorMessage = string.Format(CultureInfo.CurrentCulture, "Attempted to reuse session UID {0} already in progress", context.SessionUid.Value) });

		ProjectAssemblyRunner.PrintHeader(consoleHelper);

		return Task.FromResult(new CreateTestSessionResult { IsSuccess = true });
	}

	/// <inheritdoc/>
	public async Task ExecuteRequestAsync(ExecuteRequestContext context)
	{
		Guard.ArgumentNotNull(context);

		if (context.Request is DiscoverTestExecutionRequest discoverRequest)
			await OnDiscover(context, discoverRequest);
		else if (context.Request is RunTestExecutionRequest executionRequest)
			await OnExecute(context, executionRequest);
	}

	/// <inheritdoc/>
	public Task<bool> IsEnabledAsync() =>
		Task.FromResult(true);

	ValueTask OnDiscover(
		ExecuteRequestContext requestContext,
		DiscoverTestExecutionRequest request) =>
			OnRequest(requestContext, async (projectRunner, pipelineStartup) =>
			{
				var messageHandler = new TestPlatformDiscoveryMessageSink(innerSink, requestContext, request, projectAssembly.AssemblyFileName);
				await projectRunner.Discover(projectAssembly, pipelineStartup, messageHandler);
			});

	ValueTask OnExecute(
		ExecuteRequestContext requestContext,
		RunTestExecutionRequest request) =>
			OnRequest(requestContext, async (projectRunner, pipelineStartup) =>
			{
				var messageHandler = new TestPlatformExecutionMessageSink(innerSink, requestContext, request);
				await projectRunner.Run(projectAssembly, messageHandler, pipelineStartup);
			});

	async ValueTask OnRequest(
		ExecuteRequestContext context,
		Func<ProjectAssemblyRunner, ITestPipelineStartup?, ValueTask> callback)
	{
		if (!operationCounterBySessionUid.TryGetValue(context.Request.Session.SessionUid, out var operationCounter))
			throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Attempt to execute request against unknown session UID {0}", context.Request.Session.SessionUid), nameof(context));

		operationCounter.AddCount();

		try
		{
			var diagnosticMessages = projectAssembly.Configuration.DiagnosticMessagesOrDefault;
			var internalDiagnosticMessages = projectAssembly.Configuration.InternalDiagnosticMessagesOrDefault;
			var pipelineStartup = await ProjectAssemblyRunner.InvokePipelineStartup(testAssembly, consoleHelper, automatedMode: AutomatedMode.Off, noColor: true, diagnosticMessages, internalDiagnosticMessages);

			try
			{
				var projectRunner = new ProjectAssemblyRunner(testAssembly, consoleHelper, () => context.CancellationToken.IsCancellationRequested, automatedMode: AutomatedMode.Off);
				await callback(projectRunner, pipelineStartup);
				context.Complete();
			}
			finally
			{
				if (pipelineStartup is not null)
					await pipelineStartup.StopAsync();
			}
		}
		finally
		{
			operationCounter.Signal();
		}
	}

	/// <summary>
	/// Registers xUnit.net test support with the <paramref name="testApplicationBuilder"/>.
	/// </summary>
	/// <param name="innerSink">The inner sink to send messages to</param>
	/// <param name="testApplicationBuilder">The test application builder</param>
	/// <param name="projectAssembly">The test project assembly</param>
	/// <param name="testAssembly">The test assembly</param>
	/// <param name="consoleHelper">The console helper to send output to</param>
	public static void Register(
		IMessageSink innerSink,
		ITestApplicationBuilder testApplicationBuilder,
		XunitProjectAssembly projectAssembly,
		Assembly testAssembly,
		ConsoleHelper consoleHelper)
	{
		Guard.ArgumentNotNull(innerSink);
		Guard.ArgumentNotNull(testApplicationBuilder);
		Guard.ArgumentNotNull(projectAssembly);
		Guard.ArgumentNotNull(testAssembly);
		Guard.ArgumentNotNull(consoleHelper);

		var extension = new TestPlatformTestFramework(innerSink, projectAssembly, testAssembly, consoleHelper);
		testApplicationBuilder.RegisterTestFramework(
			serviceProvider => new TestFrameworkCapabilities(),
			(capabilities, serviceProvider) => extension
		);
	}

	/// <summary>
	/// Runs the test project.
	/// </summary>
	/// <param name="args">The command line arguments that were passed to the executable</param>
	/// <param name="extensionRegistration">The extension registration callback</param>
	/// <returns>The return code to be returned from Main</returns>
	public static async Task<int> RunAsync(
		string[] args,
		Action<ITestApplicationBuilder, string[]> extensionRegistration)
	{
		Guard.ArgumentNotNull(args);
		Guard.ArgumentNotNull(extensionRegistration);

		var consoleHelper = new ConsoleHelper(Console.In, Console.Out);

		// Create the XunitProject and XunitProjectAssembly
		var project = new XunitProject();
		var testAssembly = Assembly.GetEntryAssembly() ?? throw new TestPipelineException("Could not find entry assembly");
		var assemblyFileName = testAssembly.GetSafeLocation();
		var targetFramework = testAssembly.GetTargetFramework();
		var projectAssembly = new XunitProjectAssembly(project, Path.GetFullPath(assemblyFileName), new(3, targetFramework)) { Assembly = testAssembly };
		ConfigReader_Json.Load(projectAssembly.Configuration, projectAssembly.AssemblyFileName);
		project.Add(projectAssembly);

		// Get a diagnostic message sink
		var diagnosticMessages = projectAssembly.Configuration.DiagnosticMessagesOrDefault;
		var internalDiagnosticMessages = projectAssembly.Configuration.InternalDiagnosticMessagesOrDefault;
		var diagnosticMessageSink = ConsoleDiagnosticMessageSink.TryCreate(consoleHelper, project.Configuration.NoColorOrDefault, diagnosticMessages, internalDiagnosticMessages);

		// Get the reporter and its message handler
		// TODO: Check for environmental reporter
		var logger = new ConsoleRunnerLogger(!project.Configuration.NoColorOrDefault, project.Configuration.UseAnsiColorOrDefault, consoleHelper, waitForAcknowledgment: false);
		var reporter = new DefaultRunnerReporter();
		var reporterMessageHandler = await reporter.CreateMessageHandler(logger, diagnosticMessageSink);

		// Construct the VSTest TestApplication and run
		var builder = await TestApplication.CreateBuilderAsync(args);
		Register(reporterMessageHandler, builder, projectAssembly, testAssembly, consoleHelper);
		extensionRegistration(builder, args);
		var app = await builder.BuildAsync();
		return await app.RunAsync();
	}
}

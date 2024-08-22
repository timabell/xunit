using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
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
	ExtensionBase, VSTest_ITestFramework, IDataProducer
{
	readonly IMessageSink? diagnosticMessageSink;
	readonly IMessageSink innerSink;
	readonly XunitProjectAssembly projectAssembly;
	readonly ConcurrentDictionary<SessionUid, CountdownEvent> operationCounterBySessionUid = new();
	readonly IRunnerLogger runnerLogger;
	readonly Assembly testAssembly;
	readonly XunitTrxCapability trxCapability;

	TestPlatformTestFramework(
		IRunnerLogger runnerLogger,
		IMessageSink innerSink,
		IMessageSink? diagnosticMessageSink,
		XunitProjectAssembly projectAssembly,
		Assembly testAssembly,
		XunitTrxCapability trxCapability) :
			base("test framework", "30ea7c6e-dd24-4152-a360-1387158cd41d")
	{
		this.runnerLogger = runnerLogger;
		this.innerSink = innerSink;
		this.diagnosticMessageSink = diagnosticMessageSink;
		this.projectAssembly = projectAssembly;
		this.testAssembly = testAssembly;
		this.trxCapability = trxCapability;
	}

	/// <inheritdoc/>
	public Type[] DataTypesProduced =>
		[typeof(SessionFileArtifact)];

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

		if (!projectAssembly.Project.Configuration.NoLogoOrDefault)
			runnerLogger.LogRaw(ProjectAssemblyRunner.Banner);

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

	ValueTask OnDiscover(
		ExecuteRequestContext requestContext,
		DiscoverTestExecutionRequest request) =>
			OnRequest(requestContext, async (projectRunner, pipelineStartup) =>
			{
				// Default to true for Testing Platform
				// TODO: We'd prefer true for Test Explorer and false for `dotnet test`
				projectAssembly.Configuration.PreEnumerateTheories ??= true;

				var messageHandler = new TestPlatformDiscoveryMessageSink(innerSink, requestContext, request, projectAssembly.Assembly!.FullName!);
				await projectRunner.Discover(projectAssembly, pipelineStartup, messageHandler);
			});

	ValueTask OnExecute(
		ExecuteRequestContext requestContext,
		RunTestExecutionRequest request) =>
			OnRequest(requestContext, async (projectRunner, pipelineStartup) =>
			{
				var testCaseIDsToRun = request.Filter switch
				{
					TestNodeUidListFilter filter => filter.TestNodeUids.Select(u => u.Value).ToHashSet(StringComparer.OrdinalIgnoreCase),
					_ => null,
				};

				// Default to true for Testing Platform
				// TODO: We'd prefer true for Test Explorer and false for `dotnet test`
				projectAssembly.Configuration.PreEnumerateTheories ??= true;

				var messageHandler = new TestPlatformExecutionMessageSink(innerSink, requestContext, request, trxCapability);
				await projectRunner.Run(projectAssembly, messageHandler, diagnosticMessageSink, runnerLogger, pipelineStartup, testCaseIDsToRun);

				foreach (var output in projectAssembly.Project.Configuration.Output)
					await requestContext.MessageBus.PublishAsync(this, new SessionFileArtifact(request.Session.SessionUid, new FileInfo(output.Value), Path.GetFileNameWithoutExtension(output.Value)));
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
			var pipelineStartup = await ProjectAssemblyRunner.InvokePipelineStartup(testAssembly, diagnosticMessageSink);

			try
			{
				var projectRunner = new ProjectAssemblyRunner(testAssembly, () => context.CancellationToken.IsCancellationRequested, automatedMode: AutomatedMode.Off);
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

		var builder = await TestApplication.CreateBuilderAsync(args);
		extensionRegistration(builder, args);

		var trxCapability = new XunitTrxCapability();

		builder.CommandLine.AddProvider(() => new CommandLineOptionsProvider());
		builder.RegisterTestFramework(
			serviceProvider => new TestFrameworkCapabilities(new XunitBannerCapability(), trxCapability),
			(capabilities, serviceProvider) =>
			{
				var logger = serviceProvider.GetLoggerFactory().CreateLogger("xUnit.net");

				// Create the XunitProject and XunitProjectAssembly
				var project = new XunitProject();
				var testAssembly = Assembly.GetEntryAssembly() ?? throw new TestPipelineException("Could not find entry assembly");
				var assemblyFileName = testAssembly.GetSafeLocation();
				var targetFramework = testAssembly.GetTargetFramework();
				var projectAssembly = new XunitProjectAssembly(project, Path.GetFullPath(assemblyFileName), new(3, targetFramework)) { Assembly = testAssembly };
				ConfigReader_Json.Load(projectAssembly.Configuration, projectAssembly.AssemblyFileName);
				project.Add(projectAssembly);

				// Read command line options
				var commandLineOptions = serviceProvider.GetCommandLineOptions();
				CommandLineOptionsProvider.Parse(serviceProvider.GetConfiguration(), commandLineOptions, projectAssembly);

				// Get a diagnostic message sink
				var diagnosticMessages = projectAssembly.Configuration.DiagnosticMessagesOrDefault;
				var internalDiagnosticMessages = projectAssembly.Configuration.InternalDiagnosticMessagesOrDefault;
				var outputDevice = serviceProvider.GetOutputDevice();
				var diagnosticMessageSink = OutputDeviceDiagnosticMessageSink.TryCreate(logger, outputDevice, diagnosticMessages, internalDiagnosticMessages);

				// Use a runner logger which reports to the MTP logger, plus an option to enable output via IOutputDevice as well
				IRunnerLogger runnerLogger = new LoggerRunnerLogger(logger);
				if (commandLineOptions.IsOptionSet("xunit-info"))
					runnerLogger = new OutputDeviceRunnerLogger(outputDevice, runnerLogger);

				// Get the reporter and its message handler
				// TODO: Check for environmental reporter
				var reporter = new DefaultRunnerReporter();
				var reporterMessageHandler = reporter.CreateMessageHandler(runnerLogger, diagnosticMessageSink).SpinWait();

				return new TestPlatformTestFramework(runnerLogger, reporterMessageHandler, diagnosticMessageSink, projectAssembly, testAssembly, trxCapability);
			}
		);

		var app = await builder.BuildAsync();
		return await app.RunAsync();
	}
}

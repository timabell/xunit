using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;
using Xunit.Runner.Common;
using Xunit.Sdk;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

[ExcludeFromCodeCoverage]
internal sealed class TestPlatformExecutionMessageSink(
	IMessageSink innerSink,
	ExecuteRequestContext requestContext,
	RunTestExecutionRequest request) :
		IMessageSink, IDataProducer
{
	readonly MessageMetadataCache metadataCache = new();

	public Type[] DataTypesProduced =>
		[typeof(TestNodeUpdateMessage)];

	public string Description =>
		"xUnit.net v3 Microsoft.Testing.Platform execution message sink";

	public string DisplayName =>
		Description;

	public string Uid =>
		"fa7e6681-c892-4741-9980-724bd818f1f1";

	public string Version =>
		ThisAssembly.AssemblyVersion;

	public Task<bool> IsEnabledAsync() =>
		Task.FromResult(true);

	public bool OnMessage(IMessageSinkMessage message)
	{
		var result = innerSink.OnMessage(message);

		return
			message.DispatchWhen<ITestAssemblyFinished>(args => metadataCache.TryRemove(args.Message)) &&
			message.DispatchWhen<ITestAssemblyStarting>(args => metadataCache.Set(args.Message)) &&
			message.DispatchWhen<ITestCaseFinished>(args => metadataCache.TryRemove(args.Message)) &&
			message.DispatchWhen<ITestCaseStarting>(args => metadataCache.Set(args.Message)) &&
			message.DispatchWhen<ITestFailed>(args => SendTestResult(args.Message)) &&
			message.DispatchWhen<ITestFinished>(args => metadataCache.TryRemove(args.Message)) &&
			message.DispatchWhen<ITestNotRun>(args => SendTestResult(args.Message)) &&
			message.DispatchWhen<ITestPassed>(args => SendTestResult(args.Message)) &&
			message.DispatchWhen<ITestSkipped>(args => SendTestResult(args.Message)) &&
			message.DispatchWhen<ITestStarting>(args => SendTestResult(args.Message)) &&
			result &&
			!requestContext.CancellationToken.IsCancellationRequested;
	}

	void SendTestResult(ITestMessage testMessage)
	{
		var testStarting = testMessage as ITestStarting;
		if (testStarting is null)
			testStarting = metadataCache.TryGetTestMetadata(testMessage) as ITestStarting;
		else
			metadataCache.Set(testStarting);

		var result = new TestNode
		{
			Uid = testMessage.TestCaseUniqueID,
			DisplayName = testStarting?.TestDisplayName ?? "<unknown test display name>",
		};

		var nodeState = testMessage switch
		{
			ITestFailed failed => failed.Cause switch
			{
				FailureCause.Assertion => new FailedTestNodeStateProperty(new XunitException(failed)),
				FailureCause.Timeout => new TimeoutTestNodeStateProperty(new XunitException(failed)),
				_ => new ErrorTestNodeStateProperty(new XunitException(failed)),
			},
			// TODO: The way explicit tests are reported may change, see https://github.com/microsoft/testfx/issues/2538
			ITestNotRun => new SkippedTestNodeStateProperty("Test was not run"),
			ITestPassed => PassedTestNodeStateProperty.CachedInstance,
			ITestSkipped skipped => new SkippedTestNodeStateProperty(skipped.Reason),
			ITestStarting => InProgressTestNodeStateProperty.CachedInstance,
			_ => default(IProperty),
		};
		if (nodeState is not null)
			result.Properties.Add(nodeState);

		if (testStarting is not null && testMessage is ITestResultMessage testResult)
			result.Properties.Add(new TimingProperty(new TimingInfo(testStarting.StartTime, testResult.FinishTime, TimeSpan.FromSeconds((double)testResult.ExecutionTime))));

		var testAssemblyMetadata = metadataCache.TryGetAssemblyMetadata(testMessage);
		var testCaseMetadata = metadataCache.TryGetTestCaseMetadata(testMessage);
		if (testAssemblyMetadata is not null && testCaseMetadata is not null)
			result.AddMetadata(testCaseMetadata, testAssemblyMetadata.AssemblyPath);

		result.SendUpdate(this, request.Session.SessionUid, requestContext);
	}
}

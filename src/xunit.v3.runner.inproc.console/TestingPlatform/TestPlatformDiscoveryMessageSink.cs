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
internal sealed class TestPlatformDiscoveryMessageSink(
	IMessageSink innerSink,
	ExecuteRequestContext requestContext,
	DiscoverTestExecutionRequest request,
	string assemblyFullName) :
		IMessageSink, IDataProducer
{
	public Type[] DataTypesProduced =>
		[typeof(TestNodeUpdateMessage)];

	public string Description =>
		"xUnit.net v3 Microsoft.Testing.Platform discovery message sink";

	public string DisplayName =>
		Description;

	public string Uid =>
		"b1ef01c2-95f4-4411-b6ef-19e290225124";

	public string Version =>
		ThisAssembly.AssemblyVersion;

	public Task<bool> IsEnabledAsync() =>
		Task.FromResult(true);

	public bool OnMessage(IMessageSinkMessage message)
	{
		var result = innerSink.OnMessage(message);

		return
			message.DispatchWhen<ITestCaseDiscovered>(OnTestCaseDiscovered) &&
			result &&
			!requestContext.CancellationToken.IsCancellationRequested;
	}

	void OnTestCaseDiscovered(MessageHandlerArgs<ITestCaseDiscovered> args)
	{
		var discovered = args.Message;

		var result = new TestNode { Uid = discovered.TestCaseUniqueID, DisplayName = discovered.TestCaseDisplayName };
		result.Properties.Add(DiscoveredTestNodeStateProperty.CachedInstance);
		result.AddMetadata(discovered, assemblyFullName);
		result.SendUpdate(this, request.Session.SessionUid, requestContext);
	}
}

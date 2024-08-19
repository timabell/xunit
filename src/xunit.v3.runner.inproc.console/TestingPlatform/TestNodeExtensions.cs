using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.TestHost;
using Xunit.Sdk;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

internal static class TestNodeExtensions
{
	public static void AddMetadata(
		this TestNode result,
		ITestCaseMetadata testCase,
		string assemblyFullName)
	{
		if (testCase.TestClassName is not null && testCase.TestMethodName is not null)
			result.Properties.Add(
				new TestMethodIdentifierProperty(
					assemblyFullName,
					testCase.TestClassNamespace ?? string.Empty,
					testCase.TestClassName,
					testCase.TestMethodName,
					testCase.TestMethodParameterTypes ?? [],
					testCase.TestMethodReturnType ?? typeof(void).SafeName()
				)
			);

		var sourceFile = testCase.SourceFilePath;
		var sourceLine = testCase.SourceLineNumber;
		if (sourceFile is not null && sourceLine.HasValue)
		{
			var linePosition = new LinePosition(sourceLine.Value, -1);
			var span = new LinePositionSpan(linePosition, linePosition);
			result.Properties.Add(new TestFileLocationProperty(sourceFile, span));
		}

		var traits = testCase.Traits;
		if (traits.Count != 0)
			foreach (var kvp in traits)
				foreach (var value in kvp.Value)
					result.Properties.Add(new TestMetadataProperty(kvp.Key, value));
	}

	public static void SendUpdate(
		this TestNode testNode,
		IDataProducer producer,
		SessionUid sessionUid,
		ExecuteRequestContext requestContext) =>
			requestContext.MessageBus.PublishAsync(producer, new TestNodeUpdateMessage(sessionUid, testNode)).SpinWait();
}

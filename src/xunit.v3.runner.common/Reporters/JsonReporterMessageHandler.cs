using System;
using System.Threading.Tasks;
using Xunit.Internal;
using Xunit.Sdk;

namespace Xunit.Runner.Common;

/// <summary>
/// An implementation of <see cref="IRunnerReporterMessageHandler" /> that supports <see cref="JsonReporter" />.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JsonReporterMessageHandler" /> class.
/// </remarks>
/// <param name="logger">The logger used to report messages</param>
public class JsonReporterMessageHandler(IRunnerLogger logger) :
	IRunnerReporterMessageHandler
{
	readonly IRunnerLogger logger = Guard.ArgumentNotNull(logger);

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		GC.SuppressFinalize(this);

		return default;
	}

	/// <inheritdoc/>
	public bool OnMessage(IMessageSinkMessage message)
	{
		try
		{
			Guard.ArgumentNotNull(message);

			logger.LogImportantMessage(message.ToJson());
			logger.WaitForAcknowledgment();
		}
		// Some messages aren't serializable; we just skip it and move on
		catch (NotSupportedException) { }

		return true;
	}
}

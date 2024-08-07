using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

[ExcludeFromCodeCoverage]
internal static class TaskExtensions
{
	public static void SpinWait(this Task task)
	{
		var spin = default(SpinWait);

		while (!task.IsCompleted)
			spin.SpinOnce();

		task.GetAwaiter().GetResult();
	}
}

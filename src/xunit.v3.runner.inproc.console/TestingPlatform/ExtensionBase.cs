using System.Threading.Tasks;
using Microsoft.Testing.Platform.Extensions;

namespace Xunit.Runner.InProc.SystemConsole.TestingPlatform;

/// <summary>
/// Base class for all Microsoft.Testing.Platform classes which are extensions.
/// </summary>
public class ExtensionBase(
	string componentName,
	string uid) :
		IExtension
{
	/// <inheritdoc/>
	public string Description { get; } =
		"xUnit.net v3 Microsoft.Testing.Platform " + componentName;

	/// <inheritdoc/>
	public string DisplayName =>
		Description;

	/// <inheritdoc/>
	public string Uid =>
		uid;

	/// <inheritdoc/>
	public string Version =>
		ThisAssembly.AssemblyVersion;

	/// <inheritdoc/>
	public Task<bool> IsEnabledAsync() =>
		Task.FromResult(true);
}

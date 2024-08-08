#if NETFRAMEWORK

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit.Internal;

public class CSharpAcceptanceTestV3Assembly(string? basePath = null) :
	CSharpDotNetFrameworkExecutable(basePath)
{
	protected override IEnumerable<string> GetAdditionalCode()
	{
		foreach (var value in base.GetAdditionalCode())
			yield return value;

		var testFolder = Path.GetDirectoryName(typeof(CSharpAcceptanceTestV3Assembly).Assembly.GetLocalCodeBase());
		Guard.NotNull(() => $"Path.GetDirectoryName(\"{typeof(CSharpAcceptanceTestV3Assembly).Assembly.GetLocalCodeBase()}\") returned null", testFolder);

		var excludeFromCodeCoveragePath = Path.Combine(testFolder, "..", "..", "..", "..", "common", "ExcludeFromCodeCoverageAttribute.cs");
		excludeFromCodeCoveragePath = Path.GetFullPath(excludeFromCodeCoveragePath);
		Guard.ArgumentValid(() => $"Cannot find '{excludeFromCodeCoveragePath}' to include into compilation", File.Exists(excludeFromCodeCoveragePath));
		yield return File.ReadAllText(excludeFromCodeCoveragePath);

		var programPath = Path.Combine(testFolder, "..", "..", "..", "..", "common.tests", "Program.cs");
		programPath = Path.GetFullPath(programPath);
		Guard.ArgumentValid(() => $"Cannot find '{programPath}' to include into compilation", File.Exists(programPath));
		yield return File.ReadAllText(programPath);
	}

	protected override IEnumerable<string> GetStandardReferences() =>
		base
			.GetStandardReferences()
			.Concat([
				"System.Threading.Tasks.Extensions.dll",
				"xunit.v3.assert.dll",
				"xunit.v3.common.dll",
				"xunit.v3.core.dll",
				"xunit.v3.runner.common.dll",
				"xunit.v3.runner.inproc.console.dll",
			]);

	public new static ValueTask<CSharpAcceptanceTestV3Assembly> Create(
		string code,
		params string[] references) =>
			CreateIn(Path.GetDirectoryName(typeof(CSharpAcceptanceTestV3Assembly).Assembly.GetLocalCodeBase())!, code, references);

	public new static async ValueTask<CSharpAcceptanceTestV3Assembly> CreateIn(
		string basePath,
		string code,
		params string[] references)
	{
		Guard.ArgumentNotNull(basePath);
		Guard.ArgumentNotNull(code);
		Guard.ArgumentNotNull(references);

		basePath = Path.GetFullPath(basePath);
		Guard.ArgumentValid(() => $"Base path '{basePath}' does not exist", Directory.Exists(basePath), nameof(basePath));

		var assembly = new CSharpAcceptanceTestV3Assembly(basePath);
		await assembly.Compile([code], references);
		return assembly;
	}
}

#endif

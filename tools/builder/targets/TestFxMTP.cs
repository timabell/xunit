using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit.BuildTools.Models;

namespace Xunit.BuildTools.Targets;

[Target(
	BuildTarget.TestFxMTP,
	BuildTarget.Build
)]
public static class TestFxMTP
{
	static readonly string refSubPath = Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar;

	public static async Task OnExecute(BuildContext context)
	{
		// ------------- AnyCPU -------------

		context.BuildStep($"Running .NET Framework tests (AnyCPU, via 'dotnet test')");

		await RunTestAssemblies(context, "dotnet", "xunit.v3.*.tests.exe", x86: false);

		// ------------- Forced x86 -------------

		// Only Windows supports side-by-side 64- and 32-bit installs of .NET SDK
		if (!context.NeedMono)
		{
			// Only run 32-bit .NET Core tests if 32-bit .NET Core is installed
			var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
			if (programFilesX86 is not null)
			{
				var x86Dotnet = Path.Combine(programFilesX86, "dotnet", "dotnet.exe");
				if (File.Exists(x86Dotnet))
				{
					context.BuildStep($"Running .NET Framework tests (x86, via 'dotnet test')");

					await RunTestAssemblies(context, x86Dotnet, "xunit.v3.*.tests.exe", x86: true);
				}
			}
		}

		// Clean out all the 'dotnet test' log files, because if we got this far everything succeeded

		foreach (var logFile in Directory.GetFiles(context.TestOutputFolder, "*.log"))
			File.Delete(logFile);
	}

	static async Task RunTestAssemblies(
		BuildContext context,
		string dotnetPath,
		string searchPattern,
		bool x86)
	{
		var binSubPath = Path.Combine("bin", context.ConfigurationText, "net4");
		var testAssemblies =
			Directory
				.GetFiles(context.BaseFolder, searchPattern, SearchOption.AllDirectories)
				.Where(x => x.Contains(binSubPath) && !x.Contains(refSubPath) && (x.Contains(".x86") == x86))
				.OrderBy(x => x);

		foreach (var testAssembly in testAssemblies)
		{
			var outputFileName = $"{Path.GetFileNameWithoutExtension(testAssembly)}-net472-{(x86 ? "x86" : "AnyCPU")}-mtp";
			var projectFolder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(testAssembly))));

			await context.Exec(dotnetPath, $"test {projectFolder} --configuration {context.ConfigurationText} --framework net472 --no-build --no-restore -- {context.TestFlagsParallelMTP}--pre-enumerate-theories on --results-directory \"{context.TestOutputFolder}\" --report-xunit --report-xunit-filename \"{outputFileName}.xml\" --report-xunit-html --report-xunit-html-filename \"{outputFileName}.html\" --report-ctrf --report-ctrf-filename \"{outputFileName}.ctrf\"", workingDirectory: context.BaseFolder);
		}
	}
}

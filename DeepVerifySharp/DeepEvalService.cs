//// File: DeepEvalService.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DeepEvalSharp;
/// <summary>
/// Provides methods to manage the Python virtual environment and to invoke DeepEval via Python.
/// </summary>
public static class DeepEvalService
{
    private static readonly string DefaultProjectDir = AppDomain.CurrentDomain.BaseDirectory; // gets bin folder
    private static readonly string DefaultVenvPath = Path.Combine(DefaultProjectDir, "venv");
    private static readonly string DefaultPythonExecutableWindows = "python.exe"; // System default Python on Windows
    private static readonly string DefaultPythonExecutableUnix = "python"; // System default Python on Linux/Mac
    
    private static bool _venvInitialized = false;
    private static readonly object _lock = new();
    private static DeepEvalConfig _config = new DeepEvalConfig();
    private static string PythonExecutable = DefaultPythonExecutableWindows;
    private static string PythonExecutableUnix = DefaultPythonExecutableUnix;

    /// <summary>
    /// Sets global configuration for the DeepEvalService.
    /// </summary>
    /// <param name="config">The configuration to use.</param>
    public static void Configure(DeepEvalConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        
        lock (_lock)
        {
            _config = config.Clone();
            // Reset initialization if paths changed
            if (_venvInitialized && !string.IsNullOrEmpty(config.VenvPath))
            {
                _venvInitialized = false;
            }
        }
    }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    /// <returns>A copy of the current configuration.</returns>
    public static DeepEvalConfig GetConfiguration()
    {
        return _config.Clone();
    }

    /// <summary>
    /// Gets the virtual environment path based on configuration.
    /// </summary>
    private static string GetVenvPath()
    {
        return string.IsNullOrEmpty(_config.VenvPath) ? DefaultVenvPath : _config.VenvPath;
    }


    /// <summary>
    /// Ensures that a Python virtual environment exists and that DeepEval is installed.
    /// </summary>
    public static async Task EnsureVirtualEnv()
    {
        if (_venvInitialized) return;
        
        lock (_lock)
        {
            if (_venvInitialized) return;
            _venvInitialized = true;
        }

        string venvPath = GetVenvPath();
        
        if (!Directory.Exists(venvPath))
        {
            if (!_config.AutoCreateVenv)
                throw new InvalidOperationException($"Virtual environment does not exist at {venvPath} and AutoCreateVenv is false.");
                
            LogMessage("Creating virtual environment for DeepEval...", LogLevel.Normal);
            await RunCommand(_config.PythonPath ?? (Environment.OSVersion.Platform == PlatformID.Win32NT ? 
                DefaultPythonExecutableWindows : DefaultPythonExecutableUnix), $"-m venv \"{venvPath}\"");

            LogMessage("Installing dependencies...", LogLevel.Normal);
            await RunCommand(GetPythonPath(), "-m pip install --upgrade pip");
            await RunCommand(GetPythonPath(), "-m pip install deepeval");
        }
        else
        {
            LogMessage("Virtual environment exists. Ensuring dependencies...", LogLevel.Verbose);
            await RunCommand(GetPythonPath(), "-m pip install --upgrade deepeval");
        }

        // Configure LLM model if specified
        if (!string.IsNullOrEmpty(_config.ModelName))
        {
            await ConfigureLocalModel();
        }
    }

    /// <summary>
    /// Configures a local LLM model for DeepEval using the settings in the configuration.
    /// </summary>
    public static async Task ConfigureLocalModel()
    {
        if (string.IsNullOrEmpty(_config.ModelName))
            throw new InvalidOperationException("ModelName is required to configure a local model");

        await EnsureVirtualEnv();

        string command = "-m deepeval set-local-model";
        command += $" --model-name={_config.ModelName}";
        
        if (!string.IsNullOrEmpty(_config.BaseUrl))
            command += $" --base-url=\"{_config.BaseUrl}\"";
            
        if (!string.IsNullOrEmpty(_config.ApiKey))
            command += $" --api-key={_config.ApiKey}";

        LogMessage($"Configuring local LLM model: {_config.ModelName}", LogLevel.Normal);
        await RunCommand(GetPythonPath(), command);
    }

    /// <summary>
    /// Logs a message according to the configured log level.
    /// </summary>
    private static void LogMessage(string message, LogLevel minimumLevel)
    {
        if (_config.LogLevel >= minimumLevel)
        {
            Console.WriteLine($"[DeepEvalSharp] {message}");
        }
    }

    /// <summary>
    /// Evaluates answer relevancy by calling DeepEval's AnswerRelevancyMetric.
    /// </summary>
    public static async Task<double> EvaluateResponse(string prompt, string response)
    {
        await EnsureVirtualEnv();
        string pythonScript = $@"
import deepeval
from deepeval.metrics import AnswerRelevancyMetric
def evaluate(prompt, response):
    metric = AnswerRelevancyMetric(threshold=0.5)
    result = metric.measure(prompt, response)
    print(result)
evaluate('{EscapeForPython(prompt)}', '{EscapeForPython(response)}')
";
        return await RunPythonScript(pythonScript);
    }

    /// <summary>
    /// Evaluates correctness by calling DeepEval’s GEval metric.
    /// </summary>
    public static async Task<double> EvaluateCorrectnessAsync(string actualOutput, string expectedOutput)
    {
        await EnsureVirtualEnv();
        string script = $@"
import deepeval
from deepeval.metrics import GEval
from deepeval.test_case import LLMTestCase, LLMTestCaseParams
test_case = LLMTestCase(
    input='',
    actual_output='{EscapeForPython(actualOutput)}',
    expected_output='{EscapeForPython(expectedOutput)}'
)
metric = GEval(
    name='Correctness',
    criteria='Determine whether the actual output is factually correct based on the expected output.',
    evaluation_params=[LLMTestCaseParams.INPUT, LLMTestCaseParams.ACTUAL_OUTPUT, LLMTestCaseParams.EXPECTED_OUTPUT],
    strict_mode=True,
    threshold=0.5
)
metric.measure(test_case)
print(metric.score)
";
        return await RunPythonScript(script);
    }

    /// <summary>
    /// Evaluates faithfulness by calling DeepEval’s FaithfulnessMetric.
    /// </summary>
    public static async Task<double> EvaluateFaithfulnessAsync(string prompt, string context, string response)
    {
        await EnsureVirtualEnv();
        string script = $@"
import deepeval
from deepeval.metrics import FaithfulnessMetric
from deepeval.test_case import LLMTestCase
test_case = LLMTestCase(
    input='{EscapeForPython(prompt)}',
    actual_output='{EscapeForPython(response)}',
    retrieval_context=['{EscapeForPython(context)}']
)
metric = FaithfulnessMetric(threshold=0.5)
metric.measure(test_case)
print(metric.score)
";
        return await RunPythonScript(script);
    }

    /// <summary>
    /// Evaluates cosine similarity (as a proxy for semantic similarity) using DeepEval.
    /// </summary>
    public static async Task<double> EvaluateSimilarityAsync(string prompt, string response)
    {
        await EnsureVirtualEnv();
        var script = $@"
from deepeval.metrics import AnswerRelevancyMetric
from deepeval.test_case import LLMTestCase
test_case = LLMTestCase(
    input='{EscapeForPython(prompt)}',
    actual_output='{EscapeForPython(response)}',
    retrieval_context=['']
)
metric = AnswerRelevancyMetric(threshold=0.5)
result = metric.measure(test_case)
print(result)
";
        return await RunPythonScript(script);
    }

    /// <summary>
    /// Runs a Python script inside the virtual environment and returns the parsed double result.
    /// </summary>
    private static async Task<double> RunPythonScript(string script)
    {
        await EnsureVirtualEnv();
        // Use -c to run the script (ensure proper escaping)
        var psiArgs = $"-c \"{script.Replace("\"", "\\\"")}\"";
        var processInfo = new ProcessStartInfo(GetPythonPath(), psiArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(processInfo);
        string stdout = await proc.StandardOutput.ReadToEndAsync();
        string stderr = await proc.StandardError.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(stderr))
            LogMessage($"Python error: {stderr.Trim()}", LogLevel.Normal);
        if (!double.TryParse(stdout.Trim(), out var score))
            throw new Exception($"Failed to parse DeepEval response. STDOUT: '{stdout}', STDERR: '{stderr}'");
        return score;
    }

    /// <summary>
    /// Determines the appropriate Python executable path.
    /// </summary>
    private static string GetPythonPath()
    {
        return Environment.OSVersion.Platform == PlatformID.Win32NT ? PythonExecutable : PythonExecutableUnix;
    }

    /// <summary>
    /// Runs a command-line process and returns its output.
    /// </summary>
    private static async Task<string> RunCommand(string command, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        LogMessage($"Running command: {command} {args}", LogLevel.Verbose);

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start the process.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        if (!string.IsNullOrEmpty(error))
            LogMessage($"Error: {error}", _config.LogLevel == LogLevel.Quiet ? LogLevel.Normal : _config.LogLevel);
        
        LogMessage($"Command output: {output}", LogLevel.Verbose);
        return output.Trim();
    }

    /// <summary>
    /// Escapes a string so it can be safely embedded in a Python script.
    /// </summary>
    private static string EscapeForPython(string input)
    {
        return input.Replace("'", "\\'").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Runs the DeepEval test suite (using the CLI “deepeval test run” command) on the given test path.
    /// </summary>
    public static async Task<int> RunTests(string testPath, string additionalArgs = "")
    {
        await EnsureVirtualEnv();
        // We invoke the CLI as: python -m deepeval test run "<testPath>" [additionalArgs]
        var psi = new ProcessStartInfo
        {
            FileName = GetPythonPath(),
            Arguments = $"-m deepeval test run \"{testPath}\" {additionalArgs}",
            RedirectStandardOutput = _config.LogLevel != LogLevel.Verbose,
            RedirectStandardError = _config.LogLevel != LogLevel.Verbose,
            UseShellExecute = false,
            CreateNoWindow = _config.LogLevel == LogLevel.Quiet
        };
        
        LogMessage($"Running DeepEval tests in path: {testPath}", LogLevel.Normal);
        using var proc = Process.Start(psi);
        await proc.WaitForExitAsync();
        LogMessage($"Tests completed with exit code: {proc.ExitCode}", LogLevel.Normal);
        return proc.ExitCode;
    }

    /// <summary>
    /// Resets the local LLM model configuration for DeepEval.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ResetDeepEvalModel()
    {
        await EnsureVirtualEnv();
        
        LogMessage("Unsetting local LLM model configuration", LogLevel.Normal);
        await RunCommand(GetPythonPath(), "-m deepeval unset-local-model");
    }
}

/// <summary>
/// Represents the input for a DeepEval metric.
/// </summary>
public class MetricInput
{
    /// <summary>
    /// The user prompt or question.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// The context (e.g. reference text) to compare against.
    /// </summary>
    public string Context { get; set; } = string.Empty;

    /// <summary>
    /// The actual output generated by an LLM.
    /// </summary>
    public string ActualOutput { get; set; } = string.Empty;

    /// <summary>
    /// The expected (ground-truth) output.
    /// </summary>
    public string ExpectedOutput { get; set; } = string.Empty;
}

/// <summary>
/// Defines the interface for a DeepEval metric.
/// </summary>
public interface IDeepEvalMetric
{
    /// <summary>
    /// The metric name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates the metric given the supplied input.
    /// </summary>
    Task<double> Evaluate(MetricInput input);
}

/// <summary>
/// Wraps the DeepEval AnswerRelevancyMetric.
/// </summary>
public class AnswerRelevancyMetric : IDeepEvalMetric
{
    public string Name => "AnswerRelevancy";
    public double Threshold { get; set; } = 0.5;

    public AnswerRelevancyMetric(double threshold = 0.5)
    {
        Threshold = threshold;
    }

    public async Task<double> Evaluate(MetricInput input)
    {
        return await DeepEvalService.EvaluateResponse(input.Prompt, input.ActualOutput);
    }
}

/// <summary>
/// Wraps the DeepEval GEval metric for correctness.
/// </summary>
public class CorrectnessMetric : IDeepEvalMetric
{
    public string Name => "Correctness";
    public double Threshold { get; set; } = 0.8;

    public CorrectnessMetric(double threshold = 0.8)
    {
        Threshold = threshold;
    }

    public async Task<double> Evaluate(MetricInput input)
    {
        return await DeepEvalService.EvaluateCorrectnessAsync(input.ActualOutput, input.ExpectedOutput);
    }
}

/// <summary>
/// Wraps the DeepEval FaithfulnessMetric.
/// </summary>
public class FaithfulnessMetric : IDeepEvalMetric
{
    public string Name => "Faithfulness";
    public double Threshold { get; set; } = 0.75;

    public FaithfulnessMetric(double threshold = 0.75)
    {
        Threshold = threshold;
    }

    public async Task<double> Evaluate(MetricInput input)
    {
        return await DeepEvalService.EvaluateFaithfulnessAsync(input.Prompt, input.Context, input.ActualOutput);
    }
}

/// <summary>
/// Wraps a cosine similarity–based metric (used as semantic similarity) via DeepEval.
/// </summary>
public class CosineSimilarityMetric : IDeepEvalMetric
{
    public string Name => "CosineSimilarity";
    public double Threshold { get; set; } = 0.85;

    public CosineSimilarityMetric(double threshold = 0.85)
    {
        Threshold = threshold;
    }

    public async Task<double> Evaluate(MetricInput input)
    {
        return await DeepEvalService.EvaluateSimilarityAsync(input.Prompt, input.ActualOutput);
    }
}

/// <summary>
/// Interface for an evaluation test.
/// </summary>
public interface IEvaluationTest
{
    EvaluationType Type { get; }
    string Name { get; }
    Task<EvaluationResult> RunAsync();
}

/// <summary>
/// Abstract base class for evaluation tests.
/// </summary>
public abstract class EvaluationTest<TConfig> : IEvaluationTest
{
    public abstract EvaluationType Type { get; }
    public string Name { get; init; }
    public TConfig Config { get; init; }

    protected EvaluationTest(string name, TConfig config)
    {
        Name = name;
        Config = config;
    }

    public abstract Task<EvaluationResult> RunAsync();
}

/// <summary>
/// Represents the result of an evaluation test.
/// </summary>
public class EvaluationResult
{
    public string TestName { get; set; }
    public double Score { get; set; }
    public bool Passed { get; set; }
    public override string ToString() =>
        $"{TestName}: Score = {Score}, Passed = {Passed}";
}
/// <summary>
/// Enumeration of evaluation test types.
/// </summary>
public enum EvaluationType
{
    Correctness,
    Faithfulness,
    SemanticSimilarity
    // Add additional types as needed.
}

/// <summary>
/// Factory class to create evaluation tests based on type.
/// </summary>
public static class EvaluationTestFactory
{
    public static IEvaluationTest CreateTest(EvaluationType type, string name, object config)
    {
        return type switch
        {
            EvaluationType.Correctness => new CorrectnessTest(name, (CorrectnessConfig)config),
            EvaluationType.Faithfulness => new FaithfulnessTest(name, (FaithfulnessConfig)config),
            EvaluationType.SemanticSimilarity => new SemanticSimilarityTest(name, (SimilarityConfig)config),
            _ => throw new NotImplementedException($"Test type {type} is not implemented")
        };
    }
}

/// <summary>
/// An evaluation test for correctness.
/// </summary>
public class CorrectnessTest : EvaluationTest<CorrectnessConfig>
{
    public override EvaluationType Type => EvaluationType.Correctness;

    public CorrectnessTest(string name, CorrectnessConfig config) : base(name, config) { }

    public override async Task<EvaluationResult> RunAsync()
    {
        var metric = new CorrectnessMetric();
        var input = new MetricInput
        {
            ActualOutput = Config.ActualOutput,
            ExpectedOutput = Config.ExpectedOutput
        };
        var score = await metric.Evaluate(input);
        return new EvaluationResult
        {
            TestName = Name,
            Score = score,
            Passed = score >= Config.PassThreshold
        };
    }
}

/// <summary>
/// An evaluation test for faithfulness.
/// </summary>
public class FaithfulnessTest : EvaluationTest<FaithfulnessConfig>
{
    public override EvaluationType Type => EvaluationType.Faithfulness;

    public FaithfulnessTest(string name, FaithfulnessConfig config) : base(name, config) { }

    public override async Task<EvaluationResult> RunAsync()
    {
        var metric = new FaithfulnessMetric();
        var input = new MetricInput
        {
            Prompt = Config.Prompt ?? "",
            Context = Config.RetrievalContext,
            ActualOutput = Config.Response
        };
        var score = await metric.Evaluate(input);
        return new EvaluationResult
        {
            TestName = Name,
            Score = score,
            Passed = score >= Config.PassThreshold
        };
    }
}

/// <summary>
/// An evaluation test for semantic similarity.
/// </summary>
public class SemanticSimilarityTest : EvaluationTest<SimilarityConfig>
{
    public override EvaluationType Type => EvaluationType.SemanticSimilarity;

    public SemanticSimilarityTest(string name, SimilarityConfig config) : base(name, config) { }

    public override async Task<EvaluationResult> RunAsync()
    {
        var metric = new CosineSimilarityMetric();
        var input = new MetricInput
        {
            Prompt = Config.ReferenceText,
            ActualOutput = Config.GeneratedText
        };
        var score = await metric.Evaluate(input);
        return new EvaluationResult
        {
            TestName = Name,
            Score = score,
            Passed = score >= Config.PassThreshold
        };
    }
}

/// <summary>
/// Configuration for a correctness evaluation test.
/// </summary>
public class CorrectnessConfig
{
    public string ExpectedOutput { get; set; }
    public string ActualOutput { get; set; }
    public double PassThreshold { get; set; } = 0.8;
}

/// <summary>
/// Configuration for a faithfulness evaluation test.
/// </summary>
public class FaithfulnessConfig
{
    public string RetrievalContext { get; set; }
    public string Response { get; set; }
    public double PassThreshold { get; set; } = 0.75;
    public string Prompt { get; set; }
}

/// <summary>
/// Configuration for a semantic similarity evaluation test.
/// </summary>
public class SimilarityConfig
{
    public string ReferenceText { get; set; }
    public string GeneratedText { get; set; }
    public double PassThreshold { get; set; } = 0.85;
}
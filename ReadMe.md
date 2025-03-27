# DeepEvalSharp

DeepEvalSharp is a .NET wrapper library for the [DeepEval](https://docs.confident-ai.com/) Python library, enabling C# and .NET developers to leverage DeepEval's powerful LLM testing and evaluation capabilities.

## Overview

DeepEvalSharp provides a bridge between .NET applications and the DeepEval Python ecosystem, allowing you to:

- Write and run LLM evaluation tests in C# code
- Integrate LLM testing into your existing .NET test frameworks
- Use all of DeepEval's metrics and evaluation methods from .NET

This library is not a port of DeepEval - it's a wrapper that manages the underlying Python environment and provides a natural C# interface to the Python functionality.

## Requirements

- .NET 6.0 or higher
- Python 3.8+ installed and available in PATH
- Internet connection (for initial setup to download Python packages)

## How It Works

DeepEvalSharp works by:

1. Creating and managing a Python virtual environment
2. Installing the DeepEval Python package and its dependencies
3. Providing a C# API that translates to Python calls
4. Handling data serialization between C# and Python
5. Reporting results back in a .NET-friendly format

When you first run tests, the library will automatically:
- Create a Python virtual environment if one doesn't exist
- Install required Python dependencies
- Set up the necessary configuration

## Getting Started

### Installation

```bash
dotnet add package DeepEvalSharp
```

### Basic Usage

```csharp
using DeepEvalSharp;
using DeepEvalSharp.Metrics;

// Create an evaluator
var evaluator = new DeepEvaluator();

// Define a test case
var testCase = new LLMTestCase
{
    Input = "What is the capital of France?",
    ActualOutput = "The capital of France is Paris.",
    ExpectedOutput = "Paris"
};

// Run a test with specific metrics
var result = await evaluator.EvaluateAsync(
    testCase,
    new FactualConsistencyMetric(),
    new AnswerRelevancyMetric()
);

// Check results
Console.WriteLine($"Factual consistency: {result.Scores["factual_consistency"]}");
Console.WriteLine($"Answer relevancy: {result.Scores["answer_relevancy"]}");
```

## Integration with Test Frameworks

### xUnit Integration

DeepEvalSharp can be easily integrated with xUnit. Example from our xUnitSamples project:

```csharp
public class LLMFactualTests
{
    [Fact]
    public async Task CapitalOfFranceIsCorrect()
    {
        var evaluator = new DeepEvaluator();
        var testCase = new LLMTestCase
        {
            Input = "What is the capital of France?",
            ActualOutput = "The capital of France is Paris.",
            ExpectedOutput = "Paris"
        };
        
        var result = await evaluator.EvaluateAsync(
            testCase,
            new FactualConsistencyMetric(threshold: 0.8)
        );
        
        Assert.True(result.Passed);
    }
}
```

### Console Application

You can also run all tests using the console example which leverages the DeepEval CLI:

```csharp
class Program
{
    static async Task Main(string[] args)
    {
        var runner = new DeepEvalRunner();
        var results = await runner.RunAllTests("path/to/test/directory");
        
        foreach (var result in results)
        {
            Console.WriteLine($"Test: {result.TestName}, Passed: {result.Passed}");
        }
    }
}
```

## Available Metrics

DeepEvalSharp supports all metrics available in DeepEval:

| Metric | Description | Example Usage |
|--------|-------------|--------------|
| `AnswerRelevancyMetric` | Evaluates if the answer is relevant to the question | `new AnswerRelevancyMetric(threshold: 0.7)` |
| `FactualConsistencyMetric` | Checks if the answer is factually consistent | `new FactualConsistencyMetric()` |
| `ContextualRelevancyMetric` | Evaluates if the answer is relevant to provided context | `new ContextualRelevancyMetric(threshold: 0.8)` |
| `ContextualPrecisionMetric` | Measures precision of the answer given the context | `new ContextualPrecisionMetric()` |
| `ContextualRecallMetric` | Measures recall of the answer given the context | `new ContextualRecallMetric()` |
| `BiasMetric` | Detects bias in the model's response | `new BiasMetric(threshold: 0.3)` |
| `ToxicityMetric` | Measures toxicity level in responses | `new ToxicityMetric()` |
| `HallucinationMetric` | Detects hallucinations in the model's output | `new HallucinationMetric(threshold: 0.2)` |

## Advanced Usage

### Custom Metrics

You can create custom metrics by implementing the `IEvaluationMetric` interface:

```csharp
public class MyCustomMetric : IEvaluationMetric
{
    public string Name => "my_custom_metric";
    public double Threshold { get; }
    
    public MyCustomMetric(double threshold = 0.7)
    {
        Threshold = threshold;
    }
    
    public object GetPythonRepresentation()
    {
        // Generate Python representation for the metric
        return new Dictionary<string, object>
        {
            ["type"] = Name,
            ["threshold"] = Threshold
        };
    }
}
```

### Configuration

You can configure how DeepEvalSharp interacts with Python and LLM models using the `DeepEvalConfig` class:

```csharp
var config = new DeepEvalConfig
{
    // Python environment configuration
    PythonPath = "/custom/path/to/python",  // Optional: Path to Python executable (defaults to system Python)
    VenvPath = "/custom/path/to/venv",      // Optional: Path to virtual environment (defaults to ./venv)
    AutoCreateVenv = true,                  // Whether to automatically create the venv if it doesn't exist
    LogLevel = LogLevel.Verbose,            // Logging verbosity: Quiet, Normal, or Verbose
    
    // LLM model configuration
    ModelName = "gpt-4",                    // Name of the LLM model to use
    BaseUrl = "https://api.openai.com/v1/", // Optional: Base URL for the API (defaults to OpenAI)
    ApiKey = "sk-..."                       // Optional: API key (by default looks for OPENAI_API_KEY env var)
};

// Apply the configuration globally
DeepEvalService.Configure(config);

// Now all evaluations will use this configuration
```

#### LLM Configuration

By default, DeepEvalSharp uses OpenAI's models for evaluations, but you can configure different models:

1. **OpenAI (default)**

```csharp
var config = new DeepEvalConfig
{
    // By default, OpenAI is used and the OPENAI_API_KEY environment variable is checked
    // You can explicitly set it:
    ModelName = "gpt-4",
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
};
```

2. **Local models (e.g., with Ollama)**

```csharp
var config = new DeepEvalConfig
{
    ModelName = "llama-3.2-1B",
    BaseUrl = "http://localhost:11434/v1/", 
    ApiKey = "fake-key"  // Many local servers don't need a real key
};
```

3. **Other hosted models (Anthropic, Azure OpenAI, etc.)**

```csharp
var config = new DeepEvalConfig
{
    ModelName = "claude-3-sonnet-20240229",
    BaseUrl = "https://api.anthropic.com/v1/",
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
};
```

#### Configuration in Test Frameworks

When using with test frameworks like xUnit, you can set the configuration in the test class constructor:

```csharp
public class LLMTests : IDisposable
{
    public LLMTests()
    {
        // Configure for all tests in this class
        DeepEvalService.Configure(new DeepEvalConfig
        {
            ModelName = "llama-3.2-1B",
            BaseUrl = "http://localhost:11434/v1/",
            LogLevel = LogLevel.Quiet // Reduce noise during tests
        });
    }
    
    public void Dispose()
    {
        // Reset to default configuration if needed
        DeepEvalService.Configure(new DeepEvalConfig());
    }
    
    // Your test methods...
}
```

#### Reset LLM Model

You can reset the DeepEval model configuration back to defaults:

```csharp
await DeepEvalService.ResetDeepEvalModel();
```

## How Python Integration Works

DeepEvalSharp creates a Python virtual environment in a subdirectory of your project (or at a specified location). When tests run:

1. The library checks if the virtual environment exists and creates it if necessary
2. It installs the required Python packages using pip
3. It serializes your test case and metrics to a format Python can understand
4. It runs the Python DeepEval library with your inputs
5. It captures and parses the output, returning structured results

This approach provides the full power of DeepEval without requiring you to write Python code.

## Examples

### Evaluating Factual Consistency

```csharp
var testCase = new LLMTestCase
{
    Input = "What is the largest planet in our solar system?",
    ActualOutput = "Jupiter is the largest planet in our solar system.",
    ExpectedOutput = "Jupiter"
};

var result = await evaluator.EvaluateAsync(
    testCase,
    new FactualConsistencyMetric(threshold: 0.8)
);
```

### Testing with Context

```csharp
var testCase = new LLMTestCase
{
    Input = "Based on the information provided, what powers the spacecraft?",
    ActualOutput = "The spacecraft is powered by nuclear reactors.",
    ExpectedOutput = "Nuclear power",
    Context = "The spacecraft uses advanced nuclear reactors to generate the enormous power needed for deep space travel."
};

var result = await evaluator.EvaluateAsync(
    testCase,
    new ContextualRelevancyMetric(),
    new ContextualPrecisionMetric()
);
```

### Running Multiple Metrics

```csharp
var testCase = new LLMTestCase { /* ... */ };

var result = await evaluator.EvaluateAsync(
    testCase,
    new FactualConsistencyMetric(),
    new BiasMetric(),
    new ToxicityMetric(),
    new HallucinationMetric()
);

foreach (var score in result.Scores)
{
    Console.WriteLine($"{score.Key}: {score.Value}");
}
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgements

- [DeepEval by Confident AI](https://docs.confident-ai.com/) - The underlying Python library that powers this wrapper

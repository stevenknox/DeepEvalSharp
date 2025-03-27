using DeepEvalSharp;

// Configure DeepEval with custom settings
var config = new DeepEvalConfig
{
    ModelName = "llama-3.2-1b-instruct",
    BaseUrl = "http://localhost:1234/v1/",
    ApiKey = "fake-key"
};

// Apply the configuration
DeepEvalService.Configure(config);

Console.WriteLine("Running DeepEval test suite via RunTests()...");
// Assuming you have Python test files in a folder named "DeepEvalTests" (adjust the path as needed)
string testPath = "DeepEvalTests";
int exitCode = await DeepEvalService.RunTests(testPath);
Console.WriteLine($"DeepEval test run completed with exit code {exitCode}.");

// Demonstrate running a programmatic evaluation test for correctness.
var correctnessConfig = new CorrectnessConfig
{
    ActualOutput = "42",
    ExpectedOutput = "42",
    PassThreshold = 0.8
};
var correctnessTest = EvaluationTestFactory.CreateTest(EvaluationType.Correctness, "Correctness Test", correctnessConfig);
var result = await correctnessTest.RunAsync();
Console.WriteLine(result.ToString());

// await DeepEvalService.ResetDeepEvalModel();

// You can similarly run faithfulness and semantic similarity tests.
Console.WriteLine("Press any key to exit...");
Console.ReadKey();
using DeepEvalSharp;

namespace xUnitSamples;

public class DeepEvalTests : IDisposable
{
    // Setup method that configures DeepEval for all tests in this class
    public DeepEvalTests()
    {
        // Configure DeepEval for tests
        var config = new DeepEvalConfig
        {
             ModelName = "llama-3.2-1b-instruct",
             BaseUrl = "http://localhost:1234/v1/",
             ApiKey = "fake-key",
        };
        
        DeepEvalService.Configure(config);
    }
    

    [Fact]
    public async Task AnswerRelevancyMetric_ReturnsValidScore()
    {
        var metric = new AnswerRelevancyMetric(0.5);
        var input = new MetricInput
        {
            Prompt = "What is the refund policy?",
            ActualOutput = "Our refund policy allows returns within 30 days."
        };
        double score = await metric.Evaluate(input);
        Assert.InRange(score, 0.0, 1.0);
    }

    [Fact]
    public async Task CorrectnessMetric_ReturnsValidScore()
    {
        var metric = new CorrectnessMetric();
        var input = new MetricInput
        {
            ActualOutput = "42",
            ExpectedOutput = "42"
        };
        double score = await metric.Evaluate(input);
        Assert.InRange(score, 0.0, 1.0);
    }

    [Fact]
    public async Task FaithfulnessMetric_ReturnsValidScore()
    {
        var metric = new FaithfulnessMetric();
        var input = new MetricInput
        {
            Prompt = "What is our policy?",
            Context = "We offer a 30-day return policy.",
            ActualOutput = "You can return items within 30 days."
        };
        double score = await metric.Evaluate(input);
        Assert.InRange(score, 0.0, 1.0);
    }

    [Fact]
    public async Task CosineSimilarityMetric_ReturnsValidScore()
    {
        var metric = new CosineSimilarityMetric();
        var input = new MetricInput
        {
            Prompt = "Explain our refund policy.",
            ActualOutput = "We provide refunds within a 30-day window."
        };
        double score = await metric.Evaluate(input);
        Assert.InRange(score, 0.0, 1.0);
    }

    [Fact]
    public async Task EvaluationTestFactory_CreatesCorrectnessTest()
    {
        var config = new CorrectnessConfig
        {
            ActualOutput = "42",
            ExpectedOutput = "42",
            PassThreshold = 0.8
        };
        var test = EvaluationTestFactory.CreateTest(EvaluationType.Correctness, "Test Correctness", config);
        var result = await test.RunAsync();
        Assert.True(result.Score >= config.PassThreshold);
    }

    [Fact]
    public async Task EvaluationTestFactory_CreatesFaithfulnessTest()
    {
        var config = new FaithfulnessConfig
        {
            Prompt = "What is the refund policy?",
            RetrievalContext = "We offer returns within 30 days.",
            Response = "You can return products within 30 days.",
            PassThreshold = 0.75
        };
        var test = EvaluationTestFactory.CreateTest(EvaluationType.Faithfulness, "Test Faithfulness", config);
        var result = await test.RunAsync();
        Assert.True(result.Score >= config.PassThreshold);
    }

    [Fact]
    public async Task EvaluationTestFactory_CreatesSemanticSimilarityTest()
    {
        var config = new SimilarityConfig
        {
            ReferenceText = "Our refund policy allows returns within 30 days.",
            GeneratedText = "You can return items in 30 days.",
            PassThreshold = 0.85
        };
        var test = EvaluationTestFactory.CreateTest(EvaluationType.SemanticSimilarity, "Test Semantic Similarity", config);
        var result = await test.RunAsync();
        Assert.True(result.Score >= config.PassThreshold);
    }

    // [Fact]
    // public async Task ConfigureLocalModel_SetsUpModel()
    // {
    //     // Example of configuring a local model for a specific test
    //     // This test is marked as Skip since it requires a local model to be running
    //     Skip.IfNot(IsLocalModelAvailable(), "Local model is not available");
        
    //     var localConfig = new DeepEvalConfig
    //     {
    //         ModelName = "llama-2-7b",
    //         BaseUrl = "http://localhost:1234/v1",
    //         ApiKey = "ollama"
    //     };
        
    //     DeepEvalService.Configure(localConfig);
    //     await DeepEvalService.ConfigureLocalModel();
        
    //     var metric = new CorrectnessMetric();
    //     var input = new MetricInput
    //     {
    //         ActualOutput = "42",
    //         ExpectedOutput = "42"
    //     };
    //     double score = await metric.Evaluate(input);
    //     Assert.InRange(score, 0.0, 1.0);
    // }
    
    private bool IsLocalModelAvailable()
    {
        // Simple check to see if a local model might be available
        // You could expand this to actually ping the endpoint
        try {
            var client = new System.Net.Http.HttpClient();
            var response = client.SendAsync(
                new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get, 
                    "http://localhost:1234/v1/models"
                )
            ).Result;
            return response.IsSuccessStatusCode;
        }
        catch {
            return false;
        }
    }

        // Clean up after tests if needed
    public void Dispose()
    {
        // Reset to default configuration
        DeepEvalService.Configure(new DeepEvalConfig());
    }
}
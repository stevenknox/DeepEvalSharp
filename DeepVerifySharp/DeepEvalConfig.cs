using System;

namespace DeepEvalSharp
{
    /// <summary>
    /// Defines log verbosity levels for DeepEval operations.
    /// </summary>
    public enum LogLevel
    {
        Quiet,
        Normal,
        Verbose
    }

    /// <summary>
    /// Provides configuration options for DeepEval service.
    /// </summary>
    public class DeepEvalConfig
    {
        /// <summary>
        /// Path to the Python executable. If null, will use the system's default Python.
        /// </summary>
        public string PythonPath { get; set; }

        /// <summary>
        /// Path to the virtual environment. If null, will use a default location.
        /// </summary>
        public string VenvPath { get; set; }

        /// <summary>
        /// Whether to automatically create the virtual environment if it doesn't exist.
        /// </summary>
        public bool AutoCreateVenv { get; set; } = true;

        /// <summary>
        /// Log verbosity level.
        /// </summary>
        public LogLevel LogLevel { get; set; } = LogLevel.Normal;

        /// <summary>
        /// Name of the local LLM model to use.
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Base URL for the LLM API.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <summary>
        /// API key for the LLM service.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Creates a new instance of the DeepEvalConfig class with default values.
        /// </summary>
        public DeepEvalConfig()
        {
            // Default constructor with default values
        }

        /// <summary>
        /// Creates a copy of the configuration.
        /// </summary>
        /// <returns>A new DeepEvalConfig instance with the same settings.</returns>
        public DeepEvalConfig Clone()
        {
            return new DeepEvalConfig
            {
                PythonPath = this.PythonPath,
                VenvPath = this.VenvPath,
                AutoCreateVenv = this.AutoCreateVenv,
                LogLevel = this.LogLevel,
                ModelName = this.ModelName,
                BaseUrl = this.BaseUrl,
                ApiKey = this.ApiKey
            };
        }
    }
}

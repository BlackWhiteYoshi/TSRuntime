﻿using TSRuntime.Core.Configs;
using TSRuntime.Core.Parsing;

namespace TSRuntime.Core.Generation;

/// <summary>
/// Contains the 2 functions for generating the content for the class "TSRuntime" and the interface "ITSRuntime".
/// </summary>
public static partial class Generator {
    /// <summary>
    /// <para>The content for the TSRuntime file.</para>
    /// <para>It returns a constant string, so class TSRuntime is always the same.</para>
    /// </summary>
    public static string TSRuntimeContent => """
        // <auto-generated>
        #pragma warning disable
        #nullable enable annotations


        using System.Threading;
        using System.Threading.Tasks;

        namespace Microsoft.JSInterop;

        public sealed class TSRuntime : ITSRuntime, IDisposable, IAsyncDisposable
        {
            #region construction

            private readonly IJSRuntime _jsRuntime;
            IJSRuntime ITSRuntime.JsRuntime => _jsRuntime;


            public TSRuntime(IJSRuntime jsRuntime)
            {
                _jsRuntime = jsRuntime;
            }

            #endregion


            #region disposing

            private readonly CancellationTokenSource cancellationTokenSource = new();

            public void Dispose()
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();

                for (int i = 0; i < modules.Length; i++)
                {
                    Task<IJSObjectReference>? module = modules[i];

                    if (module?.IsCompletedSuccessfully == true)
                        _ = module.Result.DisposeAsync().Preserve();

                    modules[i] = null;
                }
            }

            public ValueTask DisposeAsync()
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();

                List<Task> taskList = new();
                for (int i = 0; i < modules.Length; i++)
                {
                    Task<IJSObjectReference>? module = modules[i];

                    if (module?.IsCompletedSuccessfully == true)
                    {
                        ValueTask valueTask = module.Result.DisposeAsync();
                        if (!valueTask.IsCompleted)
                            taskList.Add(valueTask.AsTask());
                    }

                    modules[i] = null;
                }

                if (taskList.Count == 0)
                    return ValueTask.CompletedTask;
                else
                    return new ValueTask(Task.WhenAll(taskList));
            }

            #endregion


            #region moduleList

            private readonly Task<IJSObjectReference>?[] modules = new Task<IJSObjectReference>?[ITSRuntime.MODULE_COUNT];
            Task<IJSObjectReference>?[] ITSRuntime.Modules => modules;

            Task<IJSObjectReference> ITSRuntime.GetOrLoadModule(int index, string url) {
                if (modules[index]?.IsCompletedSuccessfully == true)
                    return modules[index]!;

                return modules[index] = _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationTokenSource.Token, url).AsTask();
            }

            #endregion
        }
        
        """;

    /// <summary>
    /// <para>Creates the content of the interface "ITSRuntime" based on the given <see cref="TSSyntaxTree">syntaxTree</see> and <see cref="Config">config</see>.</para>
    /// <para>To avoid string allocations/concationations, the content is delivered as a stream of strings.</para>
    /// <para>This method is source-generated.</para>
    /// </summary>
    /// <param name="syntaxTree"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static partial IEnumerable<string> GetITSRuntimeContent(TSSyntaxTree syntaxTree, Config config);


    /// <summary>
    /// <para>Creates a parameter list and a argument list that can be iterated to create generated code.</para>
    /// <para>Used by the SourceGenerator.</para>
    /// </summary>
    /// <param name="function"></param>
    /// <param name="typeMap"></param>
    /// <returns></returns>
    private static (List<string> parameters, List<string> arguments) ParamterArgumentList(TSFunction function, Dictionary<string, string> typeMap) {
        List<string> parameters = new(function.ParameterList.Count * 4);
        List<string> arguments = new(function.ParameterList.Count * 2);

        if (function.ParameterList.Count > 0) {
            foreach (TSParameter parameter in function.ParameterList) {
                string mappedType = typeMap.ValueOrKey(parameter.Type);

                parameters.Add(mappedType);
                if (parameter.TypeNullable)
                    parameters.Add("?");
                if (parameter.Array)
                    parameters.Add("[]");
                if (parameter.ArrayNullable)
                    parameters.Add("?");
                parameters.Add(" ");
                parameters.Add(parameter.Name);
                parameters.Add(", ");

                arguments.Add(", ");
                arguments.Add(parameter.Name);
            }
        }

        return (parameters, arguments);
    }

    /// <summary>
    /// <para>
    /// Dictionary extension method.<br/>
    /// Retrieves the value in a Dictionary and defaults to the given key if not found.
    /// </para>
    /// <para>Used by the SourceGenerator.</para>
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    private static string ValueOrKey(this Dictionary<string, string> dictionary, string key) {
        bool success = dictionary.TryGetValue(key, out string? value);
        if (success)
            return value!;
        else
            return key;
    }
}

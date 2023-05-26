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


        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        namespace Microsoft.JSInterop;

        /// <summary>
        /// <para>An implementation for <see cref="ITSRuntime"/>.</para>
        /// <para>It manages JS-modules: It loads the modules, caches it in an array and disposing releases all modules.</para>
        /// </summary>
        public sealed class TSRuntime : ITSRuntime, IDisposable, IAsyncDisposable
        {
            #region construction

            private readonly IJSRuntime _jsRuntime;
            IJSRuntime ITSBase.JsRuntime => _jsRuntime;


            public TSRuntime(IJSRuntime jsRuntime)
            {
                _jsRuntime = jsRuntime;
            }

            #endregion


            #region disposing

            private readonly CancellationTokenSource cancellationTokenSource = new();

            /// <summary>
            /// Releases all modules asynchronously in parallel per fire and forget.
            /// </summary>
            public void Dispose()
            {
                if (cancellationTokenSource.IsCancellationRequested)
                    return;

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

            /// <summary>
            /// Releases all modules asynchronously in parallel and returns a task that completes, when all module tasks complets.
            /// </summary>
            /// <returns></returns>
            public ValueTask DisposeAsync()
            {
                if (cancellationTokenSource.IsCancellationRequested)
                    return ValueTask.CompletedTask;

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

            private readonly Task<IJSObjectReference>?[] modules = new Task<IJSObjectReference>?[ITSBase.MODULE_COUNT];
            Task<IJSObjectReference>?[] ITSBase.Modules => modules;

            /// <summary>
            /// <para>Loads and caches the module loading Tasks in an array.</para>
            /// <para>The first time it creates the module loading Task with the given url and stores it at the given index, all subsequent calls return the stored Task.</para>
            /// </summary>
            /// <param name="index">index of the array element.</param>
            /// <param name="url">URL to fetch if module is not loaded yet.</param>
            /// <returns></returns>
            Task<IJSObjectReference> ITSBase.GetOrLoadModule(int index, string url) {
                if (modules[index]?.IsCompletedSuccessfully == true)
                    return modules[index]!;

                return modules[index] = _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationTokenSource.Token, url).AsTask();
            }

            #endregion
        }
        
        """;

    /// <summary>
    /// <para>Creates the content of the interface "ITSRuntime" based on the given <see cref="TSStructureTree">structureTree</see> and <see cref="Config">config</see>.</para>
    /// <para>To avoid string allocations/concationations, the content is delivered as a stream of strings.</para>
    /// <para>This method is source-generated.</para>
    /// </summary>
    /// <param name="structureTree"></param>
    /// <param name="config"></param>
    /// <returns></returns>
    public static partial IEnumerable<string> GetITSRuntimeContent(TSStructureTree structureTree, Config config);


    /// <summary>
    /// Retrieves the value in a Dictionary and defaults to the given key if not found.
    /// </summary>
    /// <param name="dictionary"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    private static string GetValueOrKey(this Dictionary<string, string> dictionary, string key) {
        bool success = dictionary.TryGetValue(key, out string? value);
        if (success)
            return value!;
        else
            return key;
    }
}

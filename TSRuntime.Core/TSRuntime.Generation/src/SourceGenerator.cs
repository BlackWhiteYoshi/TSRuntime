﻿using Microsoft.CodeAnalysis;

namespace TSRuntime.Generation;

[Generator]
public sealed class SourceGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        context.RegisterPostInitializationOutput((IncrementalGeneratorPostInitializationContext context) => {
            string source = $$"""
                // <auto-generated>
                #pragma warning disable
                #nullable enable annotations
        
        
                using TSRuntime.Core.Configs;
                using TSRuntime.Core.Parsing;
        
                namespace TSRuntime.Core.Generation;
        
                public static partial class Generator {
                    public static partial IEnumerable<string> GetITSRuntimeContent(TSStructureTree structureTree, Config config) {
                {{Parser.Parse(Content)}}
                    }
                }

                """;

            context.AddSource("Generator.g.cs", source);
        });
    }


    private readonly string Content = $$""""
        {{NAMESPACE_USINGS}}

        ``
        if (!config.ModuleGrouping) {
        `+
        {{SingleInterface}}
        ``
        }
        `-
        ``
        else {
        `+
        {{MultipleInterfaces}}
        ``
        }
        `-
        """";

    private static string SingleInterface => $$"""
        {{ITS_BASE_SINGLE_SUMMARY}}
        public interface ITSBase {
        {{ITS_BASE_CONTRACTS}}
        }

        {{ITS_RUNTIME_SUMMARY}}
        public interface ITSRuntime : ITSBase {
        {{PRELOAD_ALL_MODULES}}
        ``
        for (int i = 0; i < structureTree.ModuleList.Count; i++) {
            TSModule module = structureTree.ModuleList[i];
            string index = i.ToString();
        `+


            #region `module.ModuleName`

        {{Module}}
            #endregion
        ``
        }
        `-
        

        {{PrivateInvokeMethods(true)}}


        {{JSRUNTIME_METHODS}}
        }
        """;

    private static string MultipleInterfaces => $$"""
        {{ITS_BASE_MULTIPLE_SUMMARY}}
        public interface ITSBase {
        {{ITS_BASE_CONTRACTS}}


        {{PrivateInvokeMethods(false)}}


        {{JSRUNTIME_METHODS}}
        }

        ``
        for (int i = 0; i < structureTree.ModuleList.Count; i++) {
            TSModule module = structureTree.ModuleList[i];
            string index = i.ToString();
        `+
        {{ITS_MODULE_SUMMARY}}
        public interface {{INTERFACE_MODULE_NAME}} : ITSBase {
        {{Module}}
        }
        
        ``
        }
        `-

        {{ITS_RUNTIME_SUMMARY}}
        public interface ITSRuntime : {{INTERFACE_MODULE_NAMES}}ITSBase {
        {{PRELOAD_ALL_MODULES}}
        }

        {{ServiceExtension}}
        """;



    #region Sections

    private const string ITS_BASE_SINGLE_SUMMARY = """
        /// <summary>
        /// <para>Basic implementation for <see cref="ITSRuntime"/>.</para>
        /// <para>It contains the contract between interface and implementation.</para>
        /// </summary>
        """;

    private const string ITS_BASE_MULTIPLE_SUMMARY = """
        /// <summary>
        /// <para>Basic implementation for <see cref="ITSRuntime"/>.</para>
        /// <para>It contains the contract between interface and implementation and contains the basic functions that are used in every module interface.</para>
        /// </summary>
        """;

    private const string ITS_BASE_CONTRACTS = """
            /// <summary>
            /// The total number of modules.
            /// </summary>
            protected const int MODULE_COUNT = `structureTree.ModuleList.Count.ToString()`;
        

            /// <summary>
            /// The JSRuntime to invoke the JS functionss.
            /// </summary>
            protected IJSRuntime JsRuntime { get; }

            /// <summary>
            /// Loads the module at the given URL and returns the loading task or retrieves the loading task at the given index.
            /// </summary>
            /// <param name="index">index to get the loading task.</param>
            /// <param name="url">URL to fetch the module.</param>
            /// <returns></returns>
            protected Task<IJSObjectReference> GetOrLoadModule(int index, string url);

            /// <summary>
            /// The loading task list.
            /// </summary>
            protected Task<IJSObjectReference>?[] Modules { get; }
        """;

    private const string ITS_MODULE_SUMMARY = """
        /// <summary>
        /// <para>Interface to JS-interop the module '`module.ModuleName`'.</para>
        /// <para>It contains an invoke-method for every js-function in '`module.ModuleName`' and a preload-method for loading '`module.ModuleName`'.</para>
        /// </summary>
        """;
    
    private const string ITS_RUNTIME_SUMMARY = """
        /// <summary>
        /// <para>Interface for JS-interop.</para>
        /// <para>It contains an invoke-method for every js-function, a preload-method for every module and a method to load all modules.</para>
        /// </summary>
        """;


    private const string NAMESPACE_USINGS = """
        // <auto-generated>
        #pragma warning disable
        #nullable enable annotations
        
        
        ``
        foreach (string usingStatement in config.UsingStatements) {
        `+
        using `usingStatement`;
        ``
        }
        `-
        using Microsoft.JSInterop.Infrastructure;
        using System.Diagnostics.CodeAnalysis;
        using System.Threading;
        using System.Threading.Tasks;
        
        namespace Microsoft.JSInterop;
        """;


    #region Preload

    private const string PRELOAD_ALL_MODULES = $$"""
            /// <summary>
            /// <para>Preloads all modules as javascript-modules.</para>
            /// <para>If already loading, it doesn't trigger a second loading and if any already loaded, these are not loaded again, so if all already loaded, it returns a completed task.</para>
            /// </summary>
            public Task `config.PreloadAllModulesName`() {
        ``
        foreach (TSModule module in structureTree.ModuleList) {
        `+
                {{PRELOAD_NAME_PATTERN}}();
        ``
        }
        `-

                return Task.WhenAll(Modules!);
            }
        """;

    private const string PRELOAD_MODULE = $"""
            /// <summary>
            /// <para>Preloads '`module.ModuleName`' (`module.ModulePath`) as javascript-module.</para>
            /// <para>If already loading, it doesn't trigger a second loading and if already loaded, it returns a completed task.</para>
            /// </summary>
            public Task {PRELOAD_NAME_PATTERN}()
                => GetOrLoadModule(`index`, "`module.ModulePath`");
        """;

    private const string PRELOAD_NAME_PATTERN = """
            ``
            foreach (string str in config.PreloadNamePattern.GetNaming(module.ModuleName))
                yield return str;
            ``
            """;

    #endregion


    #region CreateModule

    private static string? _module;
    private static string Module => _module ??= $$"""
        {{PRELOAD_MODULE}}


        ``
        foreach (TSFunction function in module.FunctionList) {
            string returnType = config.TypeMap.GetValueOrKey(function.ReturnType.Type);
            string returnModifiers = (function.ReturnType.TypeNullable, function.ReturnType.Array, function.ReturnType.ArrayNullable) switch {
                (false, false, _) => string.Empty,
                (true, false, _) => "?",
                (false, true, false) => "[]",
                (false, true, true) => "[]?",
                (true, true, false) => "?[]",
                (true, true, true) => "?[]?"
            };
        `+
        ``
        if (config.PromiseOnlyAsync && function.ReturnPromise) {
        `+
        {{Get_TrySync_Async(trySync: false)}}

        ``
        }
        `-
        ``
        else {
        `+
        ``
        if (config.InvokeFunctionSyncEnabled) {
        `+
            ``
        int lastIndex = function.ParameterList.Count;
        do {
            lastIndex--;
        `+

            /// <summary>
            /// <para>Invokes in module '`module.ModuleName`' the JS-function '`function.Name`' synchronously.</para>
            /// <para>If module is not loaded or synchronous is not supported, it fails with an exception.</para>
            /// </summary>
        {{SUMMARY_PARAMETERS}}
            /// <returns>result of the JS-function</returns>
            public `returnType``returnModifiers` {{GetFunctionNamePattern("config.InvokeFunctionActionNameSync")}}({{PARAMETERS_JOIN}})
                => Invoke<{{MAPPED_IJS_VOID_RESULT}}>(`index`, "`module.ModulePath`", "`function.Name`"{{ARGUMENTS}});
        ``
        }
        while (lastIndex >= 0 && function.ParameterList[lastIndex].Optional);
        `-
        ``
        }
        `-
        ``
        if (config.InvokeFunctionTrySyncEnabled) {
        `+

        {{Get_TrySync_Async(trySync: true)}}
        ``
        }
        `-
        ``
        if (config.InvokeFunctionAsyncEnabled) {
        `+

        {{Get_TrySync_Async(trySync: false)}}
        ``
        }
        `-
            
        ``
        }
        `-
        ``
        }
        `-
        """;

    private static string Get_TrySync_Async(bool trySync) {
        string summaryDescription;
        string methodName;
        string methodAction;
        if (trySync) {
            summaryDescription = "synchronously when supported, otherwise asynchronously";
            methodName = "config.InvokeFunctionActionNameTrySync";
            methodAction = "InvokeTrySync";
        }
        else {
            summaryDescription = "asynchronously";
            methodName = "config.InvokeFunctionActionNameAsync";
            methodAction = "InvokeAsync";
        }

        return $$"""
            ``
            int lastIndex = function.ParameterList.Count;
            do {
                lastIndex--;
            `+

                /// <summary>
                /// Invokes in module '`module.ModuleName`' the JS-function '`function.Name`' {{summaryDescription}}.
                /// </summary>
            {{SUMMARY_PARAMETERS}}
                /// <param name="cancellationToken">A cancellation token to signal the cancellation of the operation. Specifying this parameter will override any default cancellations such as due to timeouts (<see cref="JSRuntime.DefaultAsyncTimeout"/>) from being applied.</param>
                /// <returns>result of the JS-function</returns>
            ``
            if (returnType == "void") {
            `+
                public Task {{GetFunctionNamePattern(methodName)}}({{PARAMETERS}}CancellationToken cancellationToken = default) {
                    ValueTask<IJSVoidResult> task = {{methodAction}}<IJSVoidResult>(`index`, "`module.ModulePath`", "`function.Name`", cancellationToken{{ARGUMENTS}});
                    return task.IsCompleted ? Task.CompletedTask : task.AsTask();
                }
            ``
            }
            `-
            ``
            else {
            `+
                public ValueTask<`returnType``returnModifiers`> {{GetFunctionNamePattern(methodName)}}({{PARAMETERS}}CancellationToken cancellationToken = default)
                    => {{methodAction}}<`returnType``returnModifiers`>(`index`, "`module.ModulePath`", "`function.Name`", cancellationToken{{ARGUMENTS}});
            ``
            }
            `-
            ``
            }
            while (lastIndex >= 0 && function.ParameterList[lastIndex].Optional);
            `-
            """;
    }

    private static string GetFunctionNamePattern(string action) {
        return $"""
            ``
            foreach (string str in config.InvokeFunctionNamePattern.GetNaming(module.ModuleName, function.Name, {action}))
                yield return str;
            if (config.PromiseAppendAsync && function.ReturnPromise)
                yield return "Async";
            ``
            """;
    }

    #endregion


    /// <summary>
    /// Includes the private methods Invoke, InvokeTrySync, InvokeAsync
    /// </summary>
    /// <param name="privateOrProtected">true, private modifier is used; false, protected modifier is used</param>
    /// <returns></returns>
    private static string PrivateInvokeMethods(bool privateOrProtected) => $$"""
            /// <summary>
            /// <para>Invokes the specified JavaScript function in the specified module synchronously.</para>
            /// <para>If module is not loaded, it returns without any invoking. If synchronous is not supported, it fails with an exception.</para>
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="moduleUrl">complete path of the module, e.g. "/Pages/Example.razor.js"</param>
            /// <param name="identifier">name of the javascript function</param>
            /// <param name="success">false when the module is not loaded, otherwise true</param>
            /// <param name="args">parameter passing to the JS-function</param>
            /// <returns>default when the module is not loaded, otherwise result of the JS-function</returns>
            {{(privateOrProtected ? "private" : "protected")}} TResult Invoke<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TResult>(int moduleIndex, string moduleUrl, string identifier, params object?[]? args) {
                Task<IJSObjectReference> moduleTask = GetOrLoadModule(moduleIndex, moduleUrl);
                if (!moduleTask.IsCompletedSuccessfully)
                    throw new JSException("JS-module is not loaded. Use and await the Preload-method to ensure the module is loaded.");
                
                return ((IJSInProcessObjectReference)moduleTask.Result).Invoke<TResult>(identifier, args);
            }
        
            /// <summary>
            /// Invokes the specified JavaScript function in the specified module synchronously when supported, otherwise asynchronously.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="moduleUrl">complete path of the module, e.g. "/Pages/Example.razor.js"</param>
            /// <param name="identifier">name of the javascript function</param>
            /// <param name="cancellationToken">A cancellation token to signal the cancellation of the operation. Specifying this parameter will override any default cancellations such as due to timeouts (<see cref="JSRuntime.DefaultAsyncTimeout"/>) from being applied.</param>
            /// <param name="args">parameter passing to the JS-function</param>
            /// <returns></returns>
            {{(privateOrProtected ? "private" : "protected")}} async ValueTask<TValue> InvokeTrySync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(int moduleIndex, string moduleUrl, string identifier, CancellationToken cancellationToken, params object?[]? args) {
                IJSObjectReference module = await GetOrLoadModule(moduleIndex, moduleUrl);
                if (module is IJSInProcessObjectReference inProcessModule)
                    return inProcessModule.Invoke<TValue>(identifier, args);
                else
                    return await module.InvokeAsync<TValue>(identifier, cancellationToken, args);
            }
        
            /// <summary>
            /// Invokes the specified JavaScript function in the specified module asynchronously.
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="moduleUrl">complete path of the module, e.g. "/Pages/Example.razor.js"</param>
            /// <param name="identifier">name of the javascript function</param>
            /// <param name="cancellationToken">A cancellation token to signal the cancellation of the operation. Specifying this parameter will override any default cancellations such as due to timeouts (<see cref="JSRuntime.DefaultAsyncTimeout"/>) from being applied.</param>
            /// <param name="args">parameter passing to the JS-function</param>
            /// <returns></returns>
            {{(privateOrProtected ? "private" : "protected")}} async ValueTask<TValue> InvokeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(int moduleIndex, string moduleUrl, string identifier, CancellationToken cancellationToken, params object?[]? args) {
                IJSObjectReference module = await GetOrLoadModule(moduleIndex, moduleUrl);
                return await module.InvokeAsync<TValue>(identifier, cancellationToken, args);
            }
        """;


    /// <summary>
    /// Includes non typed methods
    /// </summary>
    private const string JSRUNTIME_METHODS = """
            #region JSRuntime methods
        ``
        if (config.JSRuntimeSyncEnabled) {
        `+

            /// <summary>
            /// Invokes the specified JavaScript function synchronously.
            /// </summary>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="args">JSON-serializable arguments.</param>
            public void InvokeVoid(string identifier, params object?[]? args)
                => Invoke<IJSVoidResult>(identifier, args);
        
            /// <summary>
            /// Invokes the specified JavaScript function synchronously.
            /// </summary>
            /// <typeparam name="TResult">The JSON-serializable return type.</typeparam>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="args">JSON-serializable arguments.</param>
            /// <returns>An instance of <typeparamref name="TResult"/> obtained by JSON-deserializing the return value.</returns>
            public TResult Invoke<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TResult>(string identifier, params object?[]? args)
                => ((IJSInProcessRuntime)JsRuntime).Invoke<TResult>(identifier, args);

        ``
        }
        `-
        ``
        if (config.JSRuntimeTrySyncEnabled) {
        `+

            /// <summary>
            /// This method performs synchronous, if the underlying implementation supports synchrounous interoperability.
            /// </summary>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="args">JSON-serializable arguments.</param>
            /// <returns></returns>
            public async ValueTask InvokeVoidTrySync(string identifier, params object?[]? args)
                => await InvokeTrySync<IJSVoidResult>(identifier, default, args);
        
            /// <summary>
            /// This method performs synchronous, if the underlying implementation supports synchrounous interoperability.
            /// </summary>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="cancellationToken">A cancellation token to signal the cancellation of the operation. Specifying this parameter will override any default cancellations such as due to timeouts (<see cref="JSRuntime.DefaultAsyncTimeout"/>) from being applied.</param>
            /// <param name="args">JSON-serializable arguments.</param>
            /// <returns></returns>
            public async ValueTask InvokeVoidTrySync(string identifier, CancellationToken cancellationToken, params object?[]? args)
                => await InvokeTrySync<IJSVoidResult>(identifier, cancellationToken, args);
        
            /// <summary>
            /// This method performs synchronous, if the underlying implementation supports synchrounous interoperability.
            /// </summary>
            /// <typeparam name="TValue">The JSON-serializable return type.</typeparam>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="args">JSON-serializable arguments.</param>
            /// <returns>An instance of <typeparamref name="TValue"/> obtained by JSON-deserializing the return value.</returns>
            public ValueTask<TValue> InvokeTrySync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, params object?[]? args)
                => InvokeTrySync<TValue>(identifier, default, args);
        
            /// <summary>
            /// This method performs synchronous, if the underlying implementation supports synchrounous interoperability.
            /// </summary>
            /// <typeparam name="TValue">The JSON-serializable return type.</typeparam>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="cancellationToken">A cancellation token to signal the cancellation of the operation. Specifying this parameter will override any default cancellations such as due to timeouts (<see cref="JSRuntime.DefaultAsyncTimeout"/>) from being applied.</param>
            /// <param name="args">JSON-serializable arguments.</param>
            /// <returns>An instance of <typeparamref name="TValue"/> obtained by JSON-deserializing the return value.</returns>
            public ValueTask<TValue> InvokeTrySync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(string identifier, CancellationToken cancellationToken, params object?[]? args) {
                if (JsRuntime is IJSInProcessRuntime jsInProcessRuntime)
                    return ValueTask.FromResult(jsInProcessRuntime.Invoke<TValue>(identifier, args));
                else
                    return JsRuntime.InvokeAsync<TValue>(identifier, cancellationToken, args);
            }
        
        ``
        }
        `-
        ``
        if (config.JSRuntimeAsyncEnabled) {
        `+

            /// <summary>
            /// Invokes the specified JavaScript function asynchronously.
            /// <para>
            /// <see cref="JSRuntime"/> will apply timeouts to this operation based on the value configured in <see cref="JSRuntime.DefaultAsyncTimeout"/>. To dispatch a call with a different timeout, or no timeout,
            /// consider using <see cref="InvokeVoidAsync{TValue}(string, CancellationToken, object[])" />.
            /// </para>
            /// </summary>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="args">JSON-serializable arguments.</param>
            /// <returns></returns>
            public async ValueTask InvokeVoidAsync(string identifier, params object?[]? args)
                => await InvokeAsync<IJSVoidResult>(identifier, default, args);
        
            /// <summary>
            /// Invokes the specified JavaScript function asynchronously.
            /// </summary>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="cancellationToken">
            /// A cancellation token to signal the cancellation of the operation. Specifying this parameter will override any default cancellations such as due to timeouts
            /// (<see cref="JSRuntime.DefaultAsyncTimeout"/>) from being applied.
            /// </param>
            /// <param name="args">JSON-serializable arguments.</param>
            /// <returns></returns>
            public async ValueTask InvokeVoidAsync(string identifier, CancellationToken cancellationToken, params object?[]? args)
                => await InvokeAsync<IJSVoidResult>(identifier, cancellationToken, args);
        
            /// <summary>
            /// Invokes the specified JavaScript function asynchronously.
            /// <para>
            /// <see cref="JSRuntime"/> will apply timeouts to this operation based on the value configured in <see cref="JSRuntime.DefaultAsyncTimeout"/>. To dispatch a call with a different timeout, or no timeout,
            /// consider using <see cref="InvokeAsync{TValue}(string, CancellationToken, object[])" />.
            /// </para>
            /// </summary>
            /// <typeparam name="TValue">The JSON-serializable return type.</typeparam>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="args">JSON-serializable arguments.</param>
            /// <returns>An instance of <typeparamref name="TValue"/> obtained by JSON-deserializing the return value.</returns>
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object?[]? args)
                => InvokeAsync<TValue>(identifier, default, args);
        
            /// <summary>
            /// Invokes the specified JavaScript function asynchronously.
            /// </summary>
            /// <typeparam name="TValue">The JSON-serializable return type.</typeparam>
            /// <param name="identifier">An identifier for the function to invoke. For example, the value <c>"someScope.someFunction"</c> will invoke the function <c>window.someScope.someFunction</c>.</param>
            /// <param name="cancellationToken">
            /// A cancellation token to signal the cancellation of the operation. Specifying this parameter will override any default cancellations such as due to timeouts
            /// (<see cref="JSRuntime.DefaultAsyncTimeout"/>) from being applied.
            /// </param>
            /// <param name="args">JSON-serializable arguments.</param>
            /// <returns>An instance of <typeparamref name="TValue"/> obtained by JSON-deserializing the return value.</returns>
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, params object?[]? args)
                => JsRuntime.InvokeAsync<TValue>(identifier, cancellationToken, args);

        ``
        }
        `-
            #endregion
        """;


    private const string ServiceExtension = $$"""
        ``
        if (config.ModuleGroupingServiceExtension) {
        `+
        public static class TSRuntimeServiceExctension {
            /// <summary>
            /// Registers a scoped ITSRuntime with a TSRuntime as implementation and if available, registers the module interfaces with the same TSRuntime-object.
            /// </summary>
            /// <param name="services"></param>
            /// <returns></returns>
            public static IServiceCollection AddTSRuntime(this IServiceCollection services) {
                services.AddScoped<ITSRuntime, TSRuntime>();
        ``
        if (config.ModuleGrouping) {
        `+

        ``
        foreach (TSModule module in structureTree.ModuleList) {
        `+
                services.AddScoped<{{INTERFACE_MODULE_NAME}}>(serviceProvider => serviceProvider.GetRequiredService<ITSRuntime>());
        ``
        }
        `-
        ``
        }
        `-

                return services;
            }
        }
        ``
        }
        `-
        """;

    #endregion


    #region Substitution

    private const string SUMMARY_PARAMETERS = """
        ``
        foreach (TSParameter parameter in function.ParameterList) {
        `+
            /// <param name="`parameter.Name`"></param>
        ``
        }
        `-
        """;

    
    private const string PARAMETERS_JOIN = $$"""
        ``
        if (lastIndex >= 0) {
            for (int __i = 0; __i < lastIndex; __i++) {
                TSParameter parameter = function.ParameterList[__i];

                {{PARAMETERS_INNTER}}
                yield return ", ";
            }

            {
                TSParameter parameter = function.ParameterList[lastIndex];

                {{PARAMETERS_INNTER}}
            }
        }
        ``
        """;

    private const string PARAMETERS = $$"""
        ``
        for (int __i = 0; __i <= lastIndex; __i++) {
            TSParameter parameter = function.ParameterList[__i];

            {{PARAMETERS_INNTER}}
            yield return ", ";
        }
        ``
        """;

    private const string PARAMETERS_INNTER = """
        yield return config.TypeMap.GetValueOrKey(parameter.Type);
                if (parameter.TypeNullable)
                    yield return "?";
                if (parameter.Array)
                    yield return "[]";
                if (parameter.ArrayNullable)
                    yield return "?";
                yield return " ";
                yield return parameter.Name;
        """;

    
    private const string ARGUMENTS = """
        ``
        for (int __i = 0; __i <= lastIndex; __i++) {
            yield return ", ";
            yield return function.ParameterList[__i].Name;
        }
        ``
        """;

    private const string MAPPED_IJS_VOID_RESULT = """
        ``
        if (returnType == "void")
            yield return "IJSVoidResult";
        else {
            yield return returnType;
            yield return returnModifiers;
        }
        ``
        """;


    private const string INTERFACE_MODULE_NAMES = $$"""
        ``
        foreach (TSModule module in structureTree.ModuleList) {
        ``
        {{INTERFACE_MODULE_NAME}}
        ``
            yield return ", ";
        }
        ``
        """;

    private const string INTERFACE_MODULE_NAME = """
        ``
            foreach (string str in config.ModuleGroupingNamePattern.GetNaming(module.ModuleName))
                yield return str;
        ``
        """;

    #endregion
}

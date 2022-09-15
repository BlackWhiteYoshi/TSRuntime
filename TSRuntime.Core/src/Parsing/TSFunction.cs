﻿namespace TSRuntime.Core.Parsing;

public sealed class TSFunction {
    public string Name { get; set; } = string.Empty;
    public required List<TSParameter> ParameterList { get; set; }
    public TSParameter ReturnType { get; set; } = new() { Name = "ReturnValue" };
    public bool ReturnPromise { get; set; }


    public static TSFunction? Parse(ReadOnlySpan<char> line) {
        if (!line.StartsWith("export declare function "))
            return null;

        TSFunction tsFunction = new() {
            ParameterList = new List<TSParameter>()
        };
        line = line[24..]; // skip "export declare function "


        // FunctionName
        int openBracket = IndexOf(line, '(');
        tsFunction.Name = new string(line[..openBracket]);

        line = line[(openBracket + 1)..]; // skip "("

        // Parameters
        tsFunction.ParameterList.Clear();
        if (line[0] == ')')
            line = line[3..]; // no parameters, skip "): "
        else
            while (true) {
                // parameter
                TSParameter tsParameter = new();
                tsFunction.ParameterList.Add(tsParameter);
                
                // parse Name
                int colon = IndexOf(line, ':');
                tsParameter.Name = new string(line[..colon]);
                line = line[(colon + 2)..]; // skip ": "

                // parse Type
                int parameterTypeEnd = IndexOfParameterEnd(line);
                tsParameter.ParseType(line[..parameterTypeEnd]);
                line = line[parameterTypeEnd..];

                if (line[0] == ',')
                    line = line[2..]; // skip ", "
                else {
                    line = line[3..]; // no parameters, skip "): "
                    break;
                }
            }

        // ReturnType/Promise
        int semicolon = IndexOf(line, ';');
        if (line.StartsWith("Promise<")) {
            tsFunction.ReturnPromise = true;
            line = line[8..(semicolon - 1)]; // cut "Promise<..>"
        }
        else {
            tsFunction.ReturnPromise = false;
            line = line[..semicolon];
        }
        tsFunction.ReturnType.ParseType(line);

        return tsFunction;



        static int IndexOf(ReadOnlySpan<char> str, char c) {
            int pos = str.IndexOf(c);
            if (pos != -1)
                return pos;
            else
                throw new Exception($"invalid d.ts file: '{c}' expected");
        }

        static int IndexOfParameterEnd(ReadOnlySpan<char> str) {
            int bracketCount = 0;
            for (int i = 0; i < str.Length; i++) {
                char c = str[i];
                switch (c) {
                    case ',':
                        return i;
                    case '(':
                        bracketCount++;
                        break;
                    case ')':
                        if (bracketCount == 0)
                            return i;
                        bracketCount--;
                        break;
                }
            }

            throw new Exception($"invalid d.ts file: no end of parameter found, expected ',' or ')'");
        }
    }
}

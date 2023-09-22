using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cosmic.ScriptConverter;

partial class Program
{
    static readonly string[] scriptOperators = new string[] { "==", ">=", "<=", "!=", ">", "<", };

    static bool TryParseInt(string maybeInt)
    {
        if (maybeInt[0] == '-')
        {
            maybeInt = maybeInt[1..];
        }
        if (maybeInt.StartsWith("0x"))
        {
            maybeInt = maybeInt[2..];
        }
        return int.TryParse(maybeInt, out _) || int.TryParse(maybeInt, NumberStyles.HexNumber, null, out _);
    }

    enum IndentApplyType
    {
        None,
        IndentBefore,
        IndentAfter,
        DedentBefore,
        DedentAfter,
        IndentJustThisLine,
        DedentJustThisLine,
        NoIndentJustThisLine
    }

    static void ConvertToNewScript(string scriptName)
    {
        var scriptPath = "Scripts/" + scriptName;
        if (!File.Exists(scriptPath))
        {
            return;
        }
        var fullScriptText = File.ReadAllLines(scriptPath);
        var convertedScript = new StringBuilder();
        var indentLevel = 0;
        var functionForwardDecList = new List<string>();
        for (var scriptLineIndex = 0; scriptLineIndex < fullScriptText.Length; scriptLineIndex++)
        {
            if (scriptLineIndex + 1 < fullScriptText.Length && fullScriptText[scriptLineIndex + 1].Contains("Editor Subs"))
            {
                break;
            }
            var line = fullScriptText[scriptLineIndex];
            var convertedLine = line.Trim();
            var commentString = string.Empty;
            var indentApplyType = IndentApplyType.None;
            if (convertedLine.Contains("//"))
            {
                commentString = convertedLine[convertedLine.IndexOf("//")..];
                if (!convertedLine.StartsWith("//"))
                {
                    commentString = " " + commentString;
                }
                convertedLine = convertedLine.Remove(convertedLine.IndexOf("//"));
            }
            var typeNameMatch = TypeNameRegex().Match(convertedLine);
            if (typeNameMatch.Success)
            {
                convertedLine = typeNameMatch.Groups[1].Value + typeNameMatch.Groups[2].Value.Replace(" ", string.Empty) + typeNameMatch.Groups[3].Value;
            }
            if (convertedLine.StartsWith("#alias"))
            {
                var split = convertedLine["#alias".Length..].Split(':', StringSplitOptions.TrimEntries);
                var aliasName = split[1];
                var aliasedValue = split[0];
                if (TryParseInt(aliasedValue))
                {
                    convertedLine = $"const {aliasName} = {aliasedValue};";
                }
                else
                {
                    convertedLine = $"alias {aliasName} = {aliasedValue};";
                }
            }
            if (convertedLine.StartsWith("#function "))
            {
                functionForwardDecList.Add(convertedLine["#function ".Length..]);
                continue;
            }
            if (convertedLine.StartsWith("#"))
            {
                indentApplyType = IndentApplyType.NoIndentJustThisLine;
            }
            if (convertedLine.StartsWith("function "))
            {
                var functionBackup = convertedLine;
                convertedLine = string.Empty;
                if (functionForwardDecList.Contains(functionBackup["function ".Length..]))
                {
                    convertedLine += "public ";
                }
                convertedLine += functionBackup + "() {";
                indentApplyType = IndentApplyType.IndentAfter;
            }
            if (convertedLine.StartsWith("end function"))
            {
                convertedLine = "}";
                indentApplyType = IndentApplyType.DedentBefore;
            }
            if (convertedLine.StartsWith("sub "))
            {
                var eventName = convertedLine["sub ".Length..];
                convertedLine = $"event {eventName}() {{";
                indentApplyType = IndentApplyType.IndentAfter;
            }
            if (convertedLine.StartsWith("end sub"))
            {
                convertedLine = "}";
                indentApplyType = IndentApplyType.DedentBefore;
            }
            if (convertedLine.StartsWith("if "))
            {
                var comparison = convertedLine["if ".Length..];
                var op = scriptOperators.First(comparison.Contains);
                var lhs = comparison.Split(op)[0].Trim();
                var rhs = comparison.Split(op)[1].Trim();
                convertedLine = $"if ({lhs} {op} {rhs}) {{";
                indentApplyType = IndentApplyType.IndentAfter;
            }
            if (convertedLine.StartsWith("else"))
            {
                convertedLine = "} else {";
                indentApplyType = IndentApplyType.DedentJustThisLine;
            }
            if (convertedLine.StartsWith("end if"))
            {
                convertedLine = "}";
                indentApplyType = IndentApplyType.DedentBefore;
            }

            var tmpIndent = indentLevel;
            if (indentApplyType == IndentApplyType.NoIndentJustThisLine)
            {
                indentLevel = 0;
            }
            if (indentApplyType == IndentApplyType.IndentBefore)
            {
                indentLevel++;
            }
            if (indentApplyType == IndentApplyType.DedentBefore)
            {
                indentLevel--;
            }
            if (indentApplyType == IndentApplyType.IndentJustThisLine)
            {
                indentLevel++;
            }
            if (indentApplyType == IndentApplyType.DedentJustThisLine)
            {
                indentLevel--;
            }
            for (var i = 0; i < indentLevel; i++)
            {
                // tabs or spaces? how many ppl do i wanna make mad
                convertedScript.Append("    ");
            }
            if (indentApplyType == IndentApplyType.IndentJustThisLine)
            {
                indentLevel--;
            }
            if (indentApplyType == IndentApplyType.DedentJustThisLine)
            {
                indentLevel++;
            }
            if (indentApplyType == IndentApplyType.IndentAfter)
            {
                indentLevel++;
            }
            if (indentApplyType == IndentApplyType.DedentAfter)
            {
                indentLevel--;
            }
            if (indentApplyType == IndentApplyType.NoIndentJustThisLine)
            {
                indentLevel = tmpIndent;
            }
            convertedScript.Append(convertedLine).AppendLine(commentString);
        }
        var cosmicScriptPath = ("CosmicScripts/" + scriptName).Replace(".txt", ".csc");
        if (!Directory.Exists(Path.GetDirectoryName(cosmicScriptPath)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cosmicScriptPath)!);
        }
        File.WriteAllText(cosmicScriptPath, convertedScript.ToString());
    }

    [GeneratedRegex("^(.*TypeName\\[)(.*)(\\].*$)")]
    private static partial Regex TypeNameRegex();

    private static void Main(string[] args)
    {
        foreach (var scriptFilePath in Directory.GetFiles("Scripts", "*.txt", SearchOption.AllDirectories))
        {
            ConvertToNewScript(scriptFilePath[("Scripts".Length + 1)..]);
        }
    }
}
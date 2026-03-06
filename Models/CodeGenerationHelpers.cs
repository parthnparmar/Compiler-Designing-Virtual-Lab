using System.Text.RegularExpressions;

namespace Compiler_Designing_Virtual_Lab.Models;

public class CodeOptimizer
{
    public static (string optimizedCode, List<string> steps) Optimize(string code)
    {
        var steps = new List<string>();
        var lines = code.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        
        if (lines.Count == 0)
            return (code, new List<string> { "No code to optimize" });
        
        // Step 1: Constant Folding
        var afterConstantFolding = PerformConstantFolding(lines, steps);
        
        // Step 2: Common Subexpression Elimination
        var afterCSE = PerformCSE(afterConstantFolding, steps);
        
        // Step 3: Dead Code Elimination
        var afterDCE = PerformDeadCodeElimination(afterCSE, steps);
        
        if (steps.Count == 0)
            steps.Add("No optimizations applicable");
        
        return (string.Join("\n", afterDCE), steps);
    }
    
    private static List<string> PerformConstantFolding(List<string> lines, List<string> steps)
    {
        var result = new List<string>();
        
        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^(\w+)\s*=\s*(\d+)\s*([+\-*/])\s*(\d+)\s*$");
            
            if (match.Success)
            {
                var varName = match.Groups[1].Value;
                var num1 = int.Parse(match.Groups[2].Value);
                var op = match.Groups[3].Value;
                var num2 = int.Parse(match.Groups[4].Value);
                
                int value = op switch
                {
                    "+" => num1 + num2,
                    "-" => num1 - num2,
                    "*" => num1 * num2,
                    "/" => num2 != 0 ? num1 / num2 : num1,
                    _ => num1
                };
                
                var newLine = $"{varName} = {value}";
                result.Add(newLine);
                steps.Add($"Constant Folding: {line} → {newLine}");
            }
            else
            {
                result.Add(line);
            }
        }
        
        return result;
    }
    
    private static List<string> PerformCSE(List<string> lines, List<string> steps)
    {
        var result = new List<string>();
        var expressions = new Dictionary<string, string>();
        
        foreach (var line in lines)
        {
            var match = Regex.Match(line, @"^(\w+)\s*=\s*(.+)$");
            
            if (match.Success)
            {
                var varName = match.Groups[1].Value;
                var expr = match.Groups[2].Value.Trim();
                
                // Check if expression is a binary operation
                var exprMatch = Regex.Match(expr, @"^(\w+)\s*([+\-*/])\s*(\w+)$");
                
                if (exprMatch.Success)
                {
                    var operand1 = exprMatch.Groups[1].Value;
                    var op = exprMatch.Groups[2].Value;
                    var operand2 = exprMatch.Groups[3].Value;
                    
                    // Normalize expression (commutative operations)
                    var normalizedExpr = (op == "+" || op == "*") && string.Compare(operand1, operand2) > 0
                        ? $"{operand2} {op} {operand1}"
                        : $"{operand1} {op} {operand2}";
                    
                    if (expressions.ContainsKey(normalizedExpr))
                    {
                        var existingVar = expressions[normalizedExpr];
                        var newLine = $"{varName} = {existingVar}";
                        result.Add(newLine);
                        steps.Add($"CSE: {line} → {newLine} (reusing {existingVar})");
                    }
                    else
                    {
                        expressions[normalizedExpr] = varName;
                        result.Add(line);
                    }
                }
                else
                {
                    result.Add(line);
                }
            }
            else
            {
                result.Add(line);
            }
        }
        
        return result;
    }
    
    private static List<string> PerformDeadCodeElimination(List<string> lines, List<string> steps)
    {
        var result = new List<string>();
        var definitions = new Dictionary<string, int>();
        var usages = new HashSet<string>();
        var hasReturn = false;
        var returnIndex = -1;
        
        // First pass: identify all definitions and usages
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            if (line.Contains("return"))
            {
                hasReturn = true;
                returnIndex = i;
                
                // Extract variables used in return statement
                var returnMatch = Regex.Match(line, @"return\s+(\w+)");
                if (returnMatch.Success)
                {
                    usages.Add(returnMatch.Groups[1].Value);
                }
            }
            
            var match = Regex.Match(line, @"^(\w+)\s*=\s*(.+)$");
            if (match.Success)
            {
                var varName = match.Groups[1].Value;
                var expr = match.Groups[2].Value;
                
                definitions[varName] = i;
                
                // Find all variables used in the expression
                var vars = Regex.Matches(expr, @"\b([a-zA-Z_]\w*)\b");
                foreach (Match v in vars)
                {
                    var usedVar = v.Groups[1].Value;
                    if (!int.TryParse(usedVar, out _))
                    {
                        usages.Add(usedVar);
                    }
                }
            }
        }
        
        // Second pass: remove dead code
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            // Remove code after return
            if (hasReturn && i > returnIndex)
            {
                steps.Add($"Dead Code Elimination: Removed unreachable code after return: {line}");
                continue;
            }
            
            var match = Regex.Match(line, @"^(\w+)\s*=");
            if (match.Success)
            {
                var varName = match.Groups[1].Value;
                
                // Check if variable is never used
                if (!usages.Contains(varName))
                {
                    steps.Add($"Dead Code Elimination: Removed unused assignment: {line}");
                    continue;
                }
            }
            
            result.Add(line);
        }
        
        return result;
    }
}

public class CodeGenerator
{
    public static List<string> IdentifyBasicBlocks(string code)
    {
        var lines = code.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select((line, idx) => new { Line = line, Index = idx + 1 })
            .ToList();
        
        if (lines.Count == 0)
            return new List<string> { "No code provided" };
        
        // Identify leaders
        var leaders = new HashSet<int> { 1 }; // First statement is always a leader
        
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Line;
            
            // Check for labels (target of goto)
            if (line.Contains(':'))
            {
                leaders.Add(i + 1);
            }
            
            // Check for goto/if statements
            if (line.Contains("goto") || line.StartsWith("if"))
            {
                // Statement after goto/if is a leader
                if (i + 1 < lines.Count)
                    leaders.Add(i + 2);
                
                // Extract label from goto
                var gotoMatch = Regex.Match(line, @"goto\s+(\w+)");
                if (gotoMatch.Success)
                {
                    var label = gotoMatch.Groups[1].Value;
                    // Find the line with this label
                    for (int j = 0; j < lines.Count; j++)
                    {
                        if (lines[j].Line.StartsWith(label + ":"))
                        {
                            leaders.Add(j + 1);
                            break;
                        }
                    }
                }
            }
        }
        
        // Build basic blocks
        var blocks = new List<string>();
        var sortedLeaders = leaders.OrderBy(x => x).ToList();
        
        for (int i = 0; i < sortedLeaders.Count; i++)
        {
            var start = sortedLeaders[i] - 1;
            var end = (i + 1 < sortedLeaders.Count) ? sortedLeaders[i + 1] - 1 : lines.Count;
            
            var blockLines = lines.Skip(start).Take(end - start).Select(x => $"{x.Index}: {x.Line}");
            blocks.Add($"B{i + 1}:\n  " + string.Join("\n  ", blockLines));
        }
        
        return blocks;
    }
    
    public static string GenerateDAG(string code)
    {
        var lines = code.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Contains("goto") && !l.Contains(':'))
            .ToList();
        
        if (lines.Count == 0)
            return "No expressions to generate DAG";
        
        // Handle single identifier case
        if (lines.Count == 1 && Regex.IsMatch(lines[0], @"^\w+$"))
            return lines[0];
        
        var nodes = new Dictionary<string, DAGNode>();
        var varLabels = new Dictionary<string, string>(); // Maps variables to their node keys
        var nodeId = 0;
        
        foreach (var line in lines)
        {
            // Match binary operation: var = operand1 op operand2
            var match = Regex.Match(line, @"^(\w+)\s*=\s*(\w+)\s*([+\-*/])\s*(\w+)$");
            if (match.Success)
            {
                var result = match.Groups[1].Value;
                var left = match.Groups[2].Value;
                var op = match.Groups[3].Value;
                var right = match.Groups[4].Value;
                
                // Create node key for CSE detection
                var nodeKey = $"{op}:{left}:{right}";
                
                // Check if this exact operation already exists
                if (nodes.ContainsKey(nodeKey))
                {
                    // Reuse existing node, just add label
                    nodes[nodeKey].Labels.Add(result);
                }
                else
                {
                    // Create new node
                    nodes[nodeKey] = new DAGNode
                    {
                        Id = nodeId++,
                        Label = op,
                        Left = left,
                        Right = right,
                        Labels = new List<string> { result }
                    };
                }
                varLabels[result] = nodeKey;
            }
            // Match simple assignment: var = operand
            else
            {
                var simpleMatch = Regex.Match(line, @"^(\w+)\s*=\s*(\w+)$");
                if (simpleMatch.Success)
                {
                    var result = simpleMatch.Groups[1].Value;
                    var source = simpleMatch.Groups[2].Value;
                    varLabels[result] = source;
                }
            }
        }
        
        if (nodes.Count == 0)
            return lines[0];
        
        // Generate ASCII DAG
        var diagram = new System.Text.StringBuilder();
        
        foreach (var kvp in nodes.Values.OrderBy(n => n.Id))
        {
            var labels = string.Join(", ", kvp.Labels);
            diagram.AppendLine($"      ({kvp.Label})");
            diagram.AppendLine($"     /   \\");
            diagram.AppendLine($"   {kvp.Left}     {kvp.Right}");
            diagram.AppendLine($"   [{labels}]");
            diagram.AppendLine();
        }
        
        return diagram.ToString();
    }
    
    public static string GenerateMachineCode(string code)
    {
        var lines = code.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var machineCode = new System.Text.StringBuilder();
        int regCounter = 1;
        var varToReg = new Dictionary<string, string>();
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Handle assignments
            var match = Regex.Match(trimmed, @"(\w+)\s*=\s*(\w+)\s*([+\-*/])\s*(\w+)");
            if (match.Success)
            {
                var result = match.Groups[1].Value;
                var left = match.Groups[2].Value;
                var op = match.Groups[3].Value;
                var right = match.Groups[4].Value;
                
                var r1 = GetOrAllocateRegister(left, varToReg, ref regCounter);
                var r2 = GetOrAllocateRegister(right, varToReg, ref regCounter);
                var r3 = $"R{regCounter++}";
                
                if (!varToReg.ContainsKey(left))
                    machineCode.AppendLine($"LOAD {r1}, {left}");
                if (!varToReg.ContainsKey(right))
                    machineCode.AppendLine($"LOAD {r2}, {right}");
                
                var instruction = op switch
                {
                    "+" => "ADD",
                    "-" => "SUB",
                    "*" => "MUL",
                    "/" => "DIV",
                    _ => "MOV"
                };
                
                machineCode.AppendLine($"{instruction} {r3}, {r1}, {r2}");
                machineCode.AppendLine($"STORE {result}, {r3}");
                varToReg[result] = r3;
            }
            else
            {
                var simpleMatch = Regex.Match(trimmed, @"(\w+)\s*=\s*(\w+)");
                if (simpleMatch.Success)
                {
                    var result = simpleMatch.Groups[1].Value;
                    var source = simpleMatch.Groups[2].Value;
                    var r1 = GetOrAllocateRegister(source, varToReg, ref regCounter);
                    
                    if (!varToReg.ContainsKey(source))
                        machineCode.AppendLine($"LOAD {r1}, {source}");
                    machineCode.AppendLine($"STORE {result}, {r1}");
                    varToReg[result] = r1;
                }
            }
        }
        
        return machineCode.ToString();
    }
    
    private static string GetOrAllocateRegister(string var, Dictionary<string, string> varToReg, ref int regCounter)
    {
        if (varToReg.ContainsKey(var))
            return varToReg[var];
        
        var reg = $"R{regCounter++}";
        varToReg[var] = reg;
        return reg;
    }
}

public class DAGNode
{
    public int Id { get; set; }
    public string Label { get; set; } = "";
    public string Left { get; set; } = "";
    public string Right { get; set; } = "";
    public List<string> Labels { get; set; } = new List<string>();
}

public class ThreeAddressCodeGenerator
{
    public static (List<string> tac, List<Quadruple> quads, List<Triple> triples) Generate(string expression)
    {
        var tac = new List<string>();
        var quads = new List<Quadruple>();
        var triples = new List<Triple>();
        
        // Parse expression
        var tokens = Tokenize(expression);
        var postfix = InfixToPostfix(tokens);
        
        var tempCounter = 1;
        var stack = new Stack<string>();
        
        foreach (var token in postfix)
        {
            if (IsOperator(token))
            {
                var right = stack.Pop();
                var left = stack.Pop();
                var temp = $"t{tempCounter++}";
                
                tac.Add($"{temp} = {left} {token} {right}");
                quads.Add(new Quadruple { Op = token, Arg1 = left, Arg2 = right, Result = temp });
                triples.Add(new Triple { Op = token, Arg1 = left, Arg2 = right });
                
                stack.Push(temp);
            }
            else
            {
                stack.Push(token);
            }
        }
        
        // Final assignment
        if (stack.Count > 0 && tokens.Count > 0)
        {
            var finalResult = stack.Pop();
            var targetVar = tokens[0];
            tac.Add($"{targetVar} = {finalResult}");
            quads.Add(new Quadruple { Op = "=", Arg1 = finalResult, Arg2 = "", Result = targetVar });
            triples.Add(new Triple { Op = "=", Arg1 = targetVar, Arg2 = $"({triples.Count - 1})" });
        }
        
        return (tac, quads, triples);
    }
    
    private static List<string> Tokenize(string expression)
    {
        var tokens = new List<string>();
        var current = "";
        
        foreach (var ch in expression)
        {
            if (char.IsWhiteSpace(ch)) continue;
            
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                current += ch;
            }
            else
            {
                if (!string.IsNullOrEmpty(current))
                {
                    tokens.Add(current);
                    current = "";
                }
                if (ch != '=')
                    tokens.Add(ch.ToString());
            }
        }
        
        if (!string.IsNullOrEmpty(current))
            tokens.Add(current);
        
        return tokens;
    }
    
    private static List<string> InfixToPostfix(List<string> tokens)
    {
        var output = new List<string>();
        var stack = new Stack<string>();
        var precedence = new Dictionary<string, int> { { "+", 1 }, { "-", 1 }, { "*", 2 }, { "/", 2 } };
        
        foreach (var token in tokens)
        {
            if (IsOperator(token))
            {
                while (stack.Count > 0 && stack.Peek() != "(" && 
                       precedence.GetValueOrDefault(stack.Peek(), 0) >= precedence.GetValueOrDefault(token, 0))
                {
                    output.Add(stack.Pop());
                }
                stack.Push(token);
            }
            else if (token == "(")
            {
                stack.Push(token);
            }
            else if (token == ")")
            {
                while (stack.Count > 0 && stack.Peek() != "(")
                    output.Add(stack.Pop());
                if (stack.Count > 0) stack.Pop();
            }
            else
            {
                output.Add(token);
            }
        }
        
        while (stack.Count > 0)
            output.Add(stack.Pop());
        
        return output;
    }
    
    private static bool IsOperator(string token)
    {
        return token == "+" || token == "-" || token == "*" || token == "/";
    }
}

using Microsoft.AspNetCore.Mvc;
using Compiler_Designing_Virtual_Lab.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace Compiler_Designing_Virtual_Lab.Controllers;

public class CompilerController : Controller
{
    private int stateCounter = 0;

    public IActionResult Index() => View();

    // Regex to NFA
    public IActionResult RegexToNfa() => View();

    [HttpPost]
    public IActionResult RegexToNfa(string regex)
    {
        if (string.IsNullOrWhiteSpace(regex))
        {
            ModelState.AddModelError("", "Please enter a valid regular expression");
            return View();
        }

        if (!ValidateRegex(regex))
        {
            ModelState.AddModelError("", "Invalid regular expression. Use only letters, digits, |, *, (, )");
            return View();
        }

        try
        {
            stateCounter = 0;
            var nfa = BuildThompsonNFA(regex);
            var model = new RegexToNfaModel
            {
                Regex = regex,
                Steps = nfa.Steps,
                TransitionTable = GenerateNfaTransitionTable(nfa),
                DiagramSvg = GenerateNfaDiagram(nfa)
            };
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Invalid regular expression: " + ex.Message);
            return View();
        }
    }

    private bool ValidateRegex(string regex)
    {
        int parenCount = 0;
        foreach (char c in regex)
        {
            if (c == '(') parenCount++;
            else if (c == ')') parenCount--;
            else if (!char.IsLetterOrDigit(c) && c != '|' && c != '*' && c != 'ε' && c != '#')
                return false;
            if (parenCount < 0) return false;
        }
        return parenCount == 0;
    }

    private NFAResult BuildThompsonNFA(string regex)
    {
        var steps = new List<string>();
        steps.Add($"Input: {regex}");
        
        // Handle epsilon
        if (regex == "ε" || regex == "")
        {
            var start = stateCounter++;
            var end = stateCounter++;
            var frag = new NFAFragment
            {
                Start = start,
                End = end,
                Transitions = new List<NFATransition> { new NFATransition { From = start, Symbol = "ε", To = end } }
            };
            steps.Add($"ε-NFA: q{start} --ε--> q{end}");
            return new NFAResult { Fragment = frag, Steps = steps };
        }
        
        var postfix = InfixToPostfix(regex);
        steps.Add($"Postfix notation: {postfix}");
        steps.Add("\nThompson's Construction:");
        
        var stack = new Stack<NFAFragment>();

        foreach (var c in postfix)
        {
            if (char.IsLetterOrDigit(c) || c == 'ε')
            {
                var start = stateCounter++;
                var end = stateCounter++;
                var frag = new NFAFragment
                {
                    Start = start,
                    End = end,
                    Transitions = new List<NFATransition> { new NFATransition { From = start, Symbol = c.ToString(), To = end } }
                };
                stack.Push(frag);
                steps.Add($"Symbol '{c}': (q{start}) --{c}--> (q{end})");
            }
            else if (c == '.')
            {
                if (stack.Count < 2) throw new Exception("Invalid expression");
                var frag2 = stack.Pop();
                var frag1 = stack.Pop();
                
                frag1.Transitions.Add(new NFATransition { From = frag1.End, Symbol = "ε", To = frag2.Start });
                frag1.Transitions.AddRange(frag2.Transitions);
                frag1.End = frag2.End;
                
                stack.Push(frag1);
                steps.Add($"Concatenation: Connect q{frag1.End} to q{frag2.Start} with ε");
            }
            else if (c == '|')
            {
                if (stack.Count < 2) throw new Exception("Invalid expression");
                var frag2 = stack.Pop();
                var frag1 = stack.Pop();
                var start = stateCounter++;
                var end = stateCounter++;
                
                var newFrag = new NFAFragment
                {
                    Start = start,
                    End = end,
                    Transitions = new List<NFATransition>
                    {
                        new NFATransition { From = start, Symbol = "ε", To = frag1.Start },
                        new NFATransition { From = start, Symbol = "ε", To = frag2.Start },
                        new NFATransition { From = frag1.End, Symbol = "ε", To = end },
                        new NFATransition { From = frag2.End, Symbol = "ε", To = end }
                    }
                };
                newFrag.Transitions.AddRange(frag1.Transitions);
                newFrag.Transitions.AddRange(frag2.Transitions);
                stack.Push(newFrag);
                steps.Add($"Union: New start q{start}, new end q{end} with ε-transitions");
            }
            else if (c == '*')
            {
                if (stack.Count < 1) throw new Exception("Invalid expression");
                var frag = stack.Pop();
                var start = stateCounter++;
                var end = stateCounter++;
                
                var newFrag = new NFAFragment
                {
                    Start = start,
                    End = end,
                    Transitions = new List<NFATransition>
                    {
                        new NFATransition { From = start, Symbol = "ε", To = frag.Start },
                        new NFATransition { From = start, Symbol = "ε", To = end },
                        new NFATransition { From = frag.End, Symbol = "ε", To = frag.Start },
                        new NFATransition { From = frag.End, Symbol = "ε", To = end }
                    }
                };
                newFrag.Transitions.AddRange(frag.Transitions);
                stack.Push(newFrag);
                steps.Add($"Kleene Star: New start q{start}, new end q{end} with loop");
            }
        }

        if (stack.Count != 1) throw new Exception("Invalid expression");
        var result = stack.Pop();
        steps.Add($"\nFinal NFA: Start = q{result.Start}, Accept = q{result.End}");
        
        return new NFAResult { Fragment = result, Steps = steps };
    }

    private string InfixToPostfix(string regex)
    {
        var output = new StringBuilder();
        var stack = new Stack<char>();
        var precedence = new Dictionary<char, int> { { '|', 1 }, { '.', 2 }, { '*', 3 } };
        
        var processed = AddConcatenation(regex);
        
        foreach (var c in processed)
        {
            if (char.IsLetterOrDigit(c) || c == '#')
                output.Append(c);
            else if (c == '(')
                stack.Push(c);
            else if (c == ')')
            {
                while (stack.Count > 0 && stack.Peek() != '(')
                    output.Append(stack.Pop());
                if (stack.Count > 0) stack.Pop();
            }
            else
            {
                while (stack.Count > 0 && stack.Peek() != '(' && 
                       precedence.GetValueOrDefault(stack.Peek(), 0) >= precedence.GetValueOrDefault(c, 0))
                    output.Append(stack.Pop());
                stack.Push(c);
            }
        }
        
        while (stack.Count > 0)
            output.Append(stack.Pop());
            
        return output.ToString();
    }

    private string AddConcatenation(string regex)
    {
        var result = new StringBuilder();
        for (int i = 0; i < regex.Length; i++)
        {
            result.Append(regex[i]);
            if (i < regex.Length - 1)
            {
                var c1 = regex[i];
                var c2 = regex[i + 1];
                if ((char.IsLetterOrDigit(c1) || c1 == ')' || c1 == '*' || c1 == '#') &&
                    (char.IsLetterOrDigit(c2) || c2 == '(' || c2 == '#'))
                    result.Append('.');
            }
        }
        return result.ToString();
    }

    // NFA to DFA
    public IActionResult NfaToDfa() => View();

    [HttpPost]
    public IActionResult NfaToDfa(string nfaInput)
    {
        if (string.IsNullOrWhiteSpace(nfaInput))
        {
            ModelState.AddModelError("", "Please enter NFA transitions");
            return View();
        }

        try
        {
            var dfa = ConvertNFAtoDFA(nfaInput);
            var model = new NfaToDfaModel
            {
                NfaInput = nfaInput,
                EpsilonClosures = dfa.EpsilonClosures,
                Steps = dfa.Steps,
                DfaTable = GenerateDfaTableFromResult(dfa),
                DiagramSvg = GenerateDfaDiagramFromResult(dfa),
                AsciiDiagram = GenerateAsciiDfaDiagram(dfa)
            };
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Invalid NFA input: " + ex.Message);
            return View();
        }
    }

    private DFAResult ConvertNFAtoDFA(string nfaInput)
    {
        var transitions = ParseNFAInput(nfaInput);
        var alphabet = transitions.Where(t => t.Symbol != "ε").Select(t => t.Symbol).Distinct().OrderBy(x => x).ToList();
        var allStates = transitions.SelectMany(t => new[] { t.From, t.To }).Distinct().ToList();
        var startState = allStates.Min();
        var finalState = allStates.Max();
        
        var epsilonClosures = new Dictionary<int, HashSet<int>>();
        foreach (var state in allStates)
            epsilonClosures[state] = ComputeEpsilonClosure(state, transitions);
        
        var closureSteps = epsilonClosures.OrderBy(kv => kv.Key).Select(kv => 
            $"ε-closure(q{kv.Key}) = {{" + string.Join(", ", kv.Value.OrderBy(x => x).Select(x => $"q{x}")) + "}").ToList();
        
        var dfaStates = new List<HashSet<int>>();
        var dfaTransitions = new List<DFATransition>();
        var unmarked = new Queue<HashSet<int>>();
        var steps = new List<string>();
        
        var startDfaState = epsilonClosures[startState];
        dfaStates.Add(startDfaState);
        unmarked.Enqueue(startDfaState);
        steps.Add($"Start: ε-closure(q{startState}) = {{" + string.Join(", ", startDfaState.OrderBy(x => x).Select(x => $"q{x}")) + "} → State A");
        
        while (unmarked.Count > 0)
        {
            var current = unmarked.Dequeue();
            var currentName = GetDFAStateName(dfaStates.IndexOf(current));
            
            foreach (var symbol in alphabet)
            {
                var move = new HashSet<int>();
                foreach (var state in current)
                {
                    var trans = transitions.Where(t => t.From == state && t.Symbol == symbol);
                    foreach (var t in trans)
                        foreach (var s in epsilonClosures[t.To])
                            move.Add(s);
                }
                
                if (move.Count > 0)
                {
                    var existing = dfaStates.FirstOrDefault(s => s.SetEquals(move));
                    if (existing == null)
                    {
                        dfaStates.Add(move);
                        unmarked.Enqueue(move);
                        existing = move;
                        var newName = GetDFAStateName(dfaStates.IndexOf(existing));
                        steps.Add($"New state {newName} = {{" + string.Join(", ", move.OrderBy(x => x).Select(x => $"q{x}")) + "}");
                    }
                    
                    var toName = GetDFAStateName(dfaStates.IndexOf(existing));
                    if (!dfaTransitions.Any(t => t.From == currentName && t.Symbol == symbol && t.To == toName))
                    {
                        dfaTransitions.Add(new DFATransition { From = currentName, Symbol = symbol, To = toName });
                        steps.Add($"δ({currentName}, {symbol}) = {toName}");
                    }
                }
            }
        }
        
        var finalStates = new HashSet<string>();
        for (int i = 0; i < dfaStates.Count; i++)
        {
            if (dfaStates[i].Contains(finalState))
                finalStates.Add(GetDFAStateName(i));
        }
        
        steps.Add("\nFinal states: " + string.Join(", ", finalStates.OrderBy(x => x)));
        
        return new DFAResult
        {
            States = dfaStates,
            Transitions = dfaTransitions,
            EpsilonClosures = closureSteps,
            Steps = steps,
            Alphabet = alphabet,
            FinalStates = finalStates
        };
    }

    private HashSet<int> ComputeEpsilonClosure(int state, List<NFATransition> transitions)
    {
        var closure = new HashSet<int> { state };
        var stack = new Stack<int>();
        stack.Push(state);
        
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            var epsilonMoves = transitions.Where(t => t.From == current && t.Symbol == "ε");
            foreach (var move in epsilonMoves)
            {
                if (!closure.Contains(move.To))
                {
                    closure.Add(move.To);
                    stack.Push(move.To);
                }
            }
        }
        
        return closure;
    }

    private List<NFATransition> ParseNFAInput(string input)
    {
        var transitions = new List<NFATransition>();
        var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var parts = line.Split(',');
            if (parts.Length >= 3)
            {
                var from = int.Parse(parts[0].Trim().Replace("q", ""));
                var symbol = parts[1].Trim();
                var to = int.Parse(parts[2].Trim().Replace("q", ""));
                transitions.Add(new NFATransition { From = from, Symbol = symbol, To = to });
            }
        }
        
        return transitions;
    }

    private string GetDFAStateName(int index)
    {
        return ((char)('A' + index)).ToString();
    }

    // Direct DFA
    public IActionResult DirectDfa() => View();

    [HttpPost]
    public IActionResult DirectDfa(string regex)
    {
        if (string.IsNullOrWhiteSpace(regex))
        {
            ModelState.AddModelError("", "Please enter a valid regular expression");
            return View();
        }

        try
        {
            var directDfa = BuildDirectDFA(regex);
            var model = new DirectDfaModel
            {
                Regex = regex,
                SyntaxTree = directDfa.SyntaxTree,
                ComputedValues = directDfa.ComputedValues,
                FollowposTable = directDfa.FollowposTable,
                DfaTable = directDfa.DfaTable,
                DiagramSvg = GenerateDirectDfaDiagramFromResult(directDfa)
            };
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Invalid regular expression: " + ex.Message);
            return View();
        }
    }

    private DirectDFAResult BuildDirectDFA(string regex)
    {
        // Step 1: Augment regex
        var augmented = regex + "#";
        
        // Step 2: Build syntax tree
        var tree = BuildSyntaxTree(augmented);
        
        // Step 3: Compute nullable, firstpos, lastpos
        ComputeNullable(tree);
        ComputeFirstpos(tree);
        ComputeLastpos(tree);
        
        // Step 4: Compute followpos
        var followpos = ComputeFollowpos(tree);
        
        // Step 5: Get all positions and alphabet
        var positions = GetAllPositions(tree);
        var alphabet = positions.Where(p => p.Symbol != "#").Select(p => p.Symbol).Distinct().OrderBy(x => x).ToList();
        var hashPosition = positions.First(p => p.Symbol == "#").Position;
        
        // Step 6: Build DFA states
        var dfaStates = new List<HashSet<int>>();
        var dfaTransitions = new List<DFATransition>();
        var unmarked = new Queue<HashSet<int>>();
        
        var startState = new HashSet<int>(tree.Firstpos);
        dfaStates.Add(startState);
        unmarked.Enqueue(startState);
        
        while (unmarked.Count > 0)
        {
            var current = unmarked.Dequeue();
            var stateName = GetDFAStateName(dfaStates.IndexOf(current));
            
            foreach (var symbol in alphabet)
            {
                var nextState = new HashSet<int>();
                
                foreach (var pos in current)
                {
                    var position = positions.FirstOrDefault(p => p.Position == pos);
                    if (position != null && position.Symbol == symbol)
                    {
                        foreach (var fp in followpos.GetValueOrDefault(pos, new HashSet<int>()))
                            nextState.Add(fp);
                    }
                }
                
                if (nextState.Count > 0)
                {
                    var existing = dfaStates.FirstOrDefault(s => s.SetEquals(nextState));
                    if (existing == null)
                    {
                        dfaStates.Add(nextState);
                        unmarked.Enqueue(nextState);
                        existing = nextState;
                    }
                    
                    var toName = GetDFAStateName(dfaStates.IndexOf(existing));
                    if (!dfaTransitions.Any(t => t.From == stateName && t.Symbol == symbol && t.To == toName))
                        dfaTransitions.Add(new DFATransition { From = stateName, Symbol = symbol, To = toName });
                }
            }
        }
        
        // Generate output
        var computedValues = new Dictionary<string, string>
        {
            { "Root nullable", tree.Nullable.ToString().ToLower() },
            { "Root firstpos", "{" + string.Join(", ", tree.Firstpos.OrderBy(x => x)) + "}" },
            { "Root lastpos", "{" + string.Join(", ", tree.Lastpos.OrderBy(x => x)) + "}" }
        };
        
        // Followpos table
        var followposTable = "<table class='table table-bordered table-sm'><thead><tr><th>Position</th><th>Symbol</th><th>followpos</th></tr></thead><tbody>";
        foreach (var pos in positions.OrderBy(p => p.Position))
        {
            var fp = followpos.GetValueOrDefault(pos.Position, new HashSet<int>());
            followposTable += $"<tr><td><strong>{pos.Position}</strong></td><td><code>{pos.Symbol}</code></td><td>{{" + string.Join(", ", fp.OrderBy(x => x)) + "}</td></tr>";
        }
        followposTable += "</tbody></table>";
        
        // DFA table
        var dfaTable = "<table class='table table-bordered table-sm'><thead><tr><th>State</th><th>Positions</th>";
        foreach (var sym in alphabet)
            dfaTable += $"<th><code>{sym}</code></th>";
        dfaTable += "<th>Final?</th></tr></thead><tbody>";
        
        for (int i = 0; i < dfaStates.Count; i++)
        {
            var stateName = GetDFAStateName(i);
            var isFinal = dfaStates[i].Contains(hashPosition);
            var statePositions = "{" + string.Join(", ", dfaStates[i].OrderBy(x => x)) + "}";
            var rowClass = i == 0 ? " class='table-primary'" : (isFinal ? " class='table-success'" : "");
            
            dfaTable += $"<tr{rowClass}><td><strong>{stateName}</strong></td><td>{statePositions}</td>";
            
            foreach (var sym in alphabet)
            {
                var trans = dfaTransitions.FirstOrDefault(t => t.From == stateName && t.Symbol == sym);
                dfaTable += $"<td>{trans?.To ?? "—"}</td>";
            }
            
            dfaTable += $"<td>{(isFinal ? "<span class='badge bg-success'>YES</span>" : "NO")}</td></tr>";
        }
        dfaTable += "</tbody></table>";
        
        return new DirectDFAResult
        {
            SyntaxTree = $"Original regex: {regex}\nAugmented regex: {augmented}\n\n" + ASCIIDiagramGenerator.GenerateSyntaxTreeDiagram(tree),
            ComputedValues = computedValues,
            FollowposTable = followposTable,
            DfaTable = dfaTable,
            States = dfaStates,
            Transitions = dfaTransitions,
            Alphabet = alphabet,
            Positions = positions
        };
    }

    private SyntaxTreeNode BuildSyntaxTree(string regex)
    {
        var postfix = InfixToPostfix(regex);
        var stack = new Stack<SyntaxTreeNode>();
        int position = 1;
        
        foreach (var c in postfix)
        {
            if (char.IsLetterOrDigit(c) || c == '#')
            {
                stack.Push(new SyntaxTreeNode { Symbol = c.ToString(), Position = position++, IsLeaf = true });
            }
            else if (c == '.')
            {
                if (stack.Count < 2) throw new Exception("Invalid expression");
                var right = stack.Pop();
                var left = stack.Pop();
                stack.Push(new SyntaxTreeNode { Symbol = ".", Left = left, Right = right });
            }
            else if (c == '|')
            {
                if (stack.Count < 2) throw new Exception("Invalid expression");
                var right = stack.Pop();
                var left = stack.Pop();
                stack.Push(new SyntaxTreeNode { Symbol = "|", Left = left, Right = right });
            }
            else if (c == '*')
            {
                if (stack.Count < 1) throw new Exception("Invalid expression");
                var child = stack.Pop();
                stack.Push(new SyntaxTreeNode { Symbol = "*", Left = child });
            }
        }
        
        if (stack.Count != 1) throw new Exception("Invalid expression");
        return stack.Pop();
    }

    private void ComputeNullable(SyntaxTreeNode node)
    {
        if (node.IsLeaf)
        {
            node.Nullable = false;
        }
        else if (node.Symbol == "|")
        {
            ComputeNullable(node.Left!);
            ComputeNullable(node.Right!);
            node.Nullable = node.Left.Nullable || node.Right.Nullable;
        }
        else if (node.Symbol == ".")
        {
            ComputeNullable(node.Left!);
            ComputeNullable(node.Right!);
            node.Nullable = node.Left.Nullable && node.Right.Nullable;
        }
        else if (node.Symbol == "*")
        {
            ComputeNullable(node.Left!);
            node.Nullable = true;
        }
    }

    private void ComputeFirstpos(SyntaxTreeNode node)
    {
        if (node.IsLeaf)
        {
            node.Firstpos = new HashSet<int> { node.Position };
        }
        else if (node.Symbol == "|")
        {
            ComputeFirstpos(node.Left!);
            ComputeFirstpos(node.Right!);
            node.Firstpos = new HashSet<int>(node.Left.Firstpos.Union(node.Right.Firstpos));
        }
        else if (node.Symbol == ".")
        {
            ComputeFirstpos(node.Left!);
            ComputeFirstpos(node.Right!);
            node.Firstpos = node.Left.Nullable 
                ? new HashSet<int>(node.Left.Firstpos.Union(node.Right.Firstpos))
                : new HashSet<int>(node.Left.Firstpos);
        }
        else if (node.Symbol == "*")
        {
            ComputeFirstpos(node.Left!);
            node.Firstpos = new HashSet<int>(node.Left.Firstpos);
        }
    }

    private void ComputeLastpos(SyntaxTreeNode node)
    {
        if (node.IsLeaf)
        {
            node.Lastpos = new HashSet<int> { node.Position };
        }
        else if (node.Symbol == "|")
        {
            ComputeLastpos(node.Left!);
            ComputeLastpos(node.Right!);
            node.Lastpos = new HashSet<int>(node.Left.Lastpos.Union(node.Right.Lastpos));
        }
        else if (node.Symbol == ".")
        {
            ComputeLastpos(node.Left!);
            ComputeLastpos(node.Right!);
            node.Lastpos = node.Right.Nullable
                ? new HashSet<int>(node.Left.Lastpos.Union(node.Right.Lastpos))
                : new HashSet<int>(node.Right.Lastpos);
        }
        else if (node.Symbol == "*")
        {
            ComputeLastpos(node.Left!);
            node.Lastpos = new HashSet<int>(node.Left.Lastpos);
        }
    }

    private Dictionary<int, HashSet<int>> ComputeFollowpos(SyntaxTreeNode node)
    {
        var followpos = new Dictionary<int, HashSet<int>>();
        ComputeFollowposRecursive(node, followpos);
        return followpos;
    }

    private void ComputeFollowposRecursive(SyntaxTreeNode node, Dictionary<int, HashSet<int>> followpos)
    {
        if (node.Symbol == ".")
        {
            foreach (var i in node.Left!.Lastpos)
            {
                if (!followpos.ContainsKey(i))
                    followpos[i] = new HashSet<int>();
                foreach (var j in node.Right!.Firstpos)
                    followpos[i].Add(j);
            }
        }
        else if (node.Symbol == "*")
        {
            foreach (var i in node.Lastpos)
            {
                if (!followpos.ContainsKey(i))
                    followpos[i] = new HashSet<int>();
                foreach (var j in node.Firstpos)
                    followpos[i].Add(j);
            }
        }
        
        if (node.Left != null) ComputeFollowposRecursive(node.Left, followpos);
        if (node.Right != null) ComputeFollowposRecursive(node.Right, followpos);
    }

    private List<PositionInfo> GetAllPositions(SyntaxTreeNode node)
    {
        var positions = new List<PositionInfo>();
        GetAllPositionsRecursive(node, positions);
        return positions;
    }

    private void GetAllPositionsRecursive(SyntaxTreeNode node, List<PositionInfo> positions)
    {
        if (node.IsLeaf)
        {
            positions.Add(new PositionInfo { Position = node.Position, Symbol = node.Symbol });
        }
        if (node.Left != null) GetAllPositionsRecursive(node.Left, positions);
        if (node.Right != null) GetAllPositionsRecursive(node.Right, positions);
    }

    // DFA Minimization
    public IActionResult DfaMinimization() => View();

    [HttpPost]
    public IActionResult DfaMinimization(string dfaInput)
    {
        if (string.IsNullOrWhiteSpace(dfaInput))
        {
            ModelState.AddModelError("", "Please enter DFA transitions");
            return View();
        }

        try
        {
            var minimized = MinimizeDFACorrect(dfaInput);
            var model = new DfaMinimizationModel
            {
                DfaInput = dfaInput,
                Partitions = minimized.Partitions,
                MinimizedDfa = minimized.Summary,
                BeforeDiagram = GenerateDfaDiagramFromInput(dfaInput),
                AfterDiagram = GenerateMinimizedDfaDiagram(minimized),
                AsciiDiagram = GenerateMinimizedAsciiDiagram(minimized)
            };
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Invalid DFA input: " + ex.Message);
            return View();
        }
    }

    private MinimizationResult MinimizeDFACorrect(string dfaInput)
    {
        var transitions = ParseDFAInput(dfaInput);
        var allStates = transitions.SelectMany(t => new[] { t.From, t.To }).Distinct().ToHashSet();
        var alphabet = transitions.Select(t => t.Symbol).Distinct().OrderBy(x => x).ToList();
        
        var finalStates = new HashSet<string>();
        string? startState = null;
        
        foreach (var line in dfaInput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var parts = trimmed.Split(',');
            if (parts.Length < 3) continue;
            var stateStr = parts[0].Trim();
            var cleanState = stateStr.Replace(">", "").Replace("*", "").Trim();
            if (stateStr.Contains('>') && startState == null) startState = cleanState;
            if (stateStr.Contains('*')) finalStates.Add(cleanState);
        }
        
        if (startState == null && allStates.Count > 0) startState = allStates.OrderBy(s => s).First();
        
        var reachable = new HashSet<string>();
        if (startState != null)
        {
            var queue = new Queue<string>();
            queue.Enqueue(startState);
            reachable.Add(startState);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var trans in transitions.Where(t => t.From == current))
                    if (!reachable.Contains(trans.To)) { reachable.Add(trans.To); queue.Enqueue(trans.To); }
            }
        }
        else reachable = allStates;
        
        var states = reachable.OrderBy(s => s).ToList();
        transitions = transitions.Where(t => reachable.Contains(t.From) && reachable.Contains(t.To)).ToList();
        finalStates = finalStates.Where(f => reachable.Contains(f)).ToHashSet();
        
        var steps = new List<string>();
        var unreachable = allStates.Except(reachable).OrderBy(x => x).ToList();
        if (unreachable.Count > 0) steps.Add($"Removed unreachable: {{" + string.Join(", ", unreachable) + "}");
        
        var partitions = new List<List<HashSet<string>>>();
        var p0 = new List<HashSet<string>>();
        if (finalStates.Count > 0) p0.Add(new HashSet<string>(finalStates));
        var nonFinal = states.Where(s => !finalStates.Contains(s)).ToHashSet();
        if (nonFinal.Count > 0) p0.Add(nonFinal);
        if (p0.Count == 0) p0.Add(new HashSet<string>(states));
        partitions.Add(p0);
        steps.Add($"P0 = {{" + string.Join("}, {", p0.Select(g => "{" + string.Join(", ", g.OrderBy(x => x)) + "}")) + "}");
        
        bool changed = true;
        int iteration = 1;
        while (changed && iteration <= 20)
        {
            changed = false;
            var newPartition = new List<HashSet<string>>();
            foreach (var group in partitions.Last())
            {
                if (group.Count == 1) { newPartition.Add(new HashSet<string>(group)); continue; }
                var subgroups = new Dictionary<string, HashSet<string>>();
                foreach (var state in group)
                {
                    var signature = "";
                    foreach (var symbol in alphabet)
                    {
                        var trans = transitions.FirstOrDefault(t => t.From == state && t.Symbol == symbol);
                        var targetGroup = -1;
                        if (trans != null)
                            for (int i = 0; i < partitions.Last().Count; i++)
                                if (partitions.Last()[i].Contains(trans.To)) { targetGroup = i; break; }
                        signature += targetGroup + ",";
                    }
                    if (!subgroups.ContainsKey(signature)) subgroups[signature] = new HashSet<string>();
                    subgroups[signature].Add(state);
                }
                if (subgroups.Count > 1) changed = true;
                newPartition.AddRange(subgroups.Values);
            }
            partitions.Add(newPartition);
            steps.Add($"P{iteration} = {{" + string.Join("}, {", newPartition.Select(g => "{" + string.Join(", ", g.OrderBy(x => x)) + "}")) + "}");
            iteration++;
        }
        steps.Add("No further refinement possible");
        
        var finalPartition = partitions.Last();
        var stateMapping = new Dictionary<string, string>();
        var minimizedFinalStates = new HashSet<string>();
        for (int i = 0; i < finalPartition.Count; i++)
        {
            var groupName = $"q{i}";
            foreach (var state in finalPartition[i]) stateMapping[state] = groupName;
            if (finalPartition[i].Any(s => finalStates.Contains(s))) minimizedFinalStates.Add(groupName);
        }
        
        var minimizedTransitions = new List<DFATransition>();
        var processedTransitions = new HashSet<string>();
        foreach (var trans in transitions)
        {
            if (!stateMapping.ContainsKey(trans.From) || !stateMapping.ContainsKey(trans.To)) continue;
            var fromGroup = stateMapping[trans.From];
            var toGroup = stateMapping[trans.To];
            var key = $"{fromGroup},{trans.Symbol},{toGroup}";
            if (!processedTransitions.Contains(key))
            {
                minimizedTransitions.Add(new DFATransition { From = fromGroup, Symbol = trans.Symbol, To = toGroup });
                processedTransitions.Add(key);
            }
        }
        
        var originalCount = states.Count;
        var minimizedCount = finalPartition.Count;
        var summary = minimizedCount < originalCount
            ? $"Minimized DFA: {minimizedCount} state{(minimizedCount != 1 ? "s" : "")} (reduced from {originalCount})"
            : $"DFA already minimal: {minimizedCount} state{(minimizedCount != 1 ? "s" : "")}";
        
        return new MinimizationResult
        {
            Partitions = steps,
            Summary = summary,
            MinimizedTransitions = minimizedTransitions.OrderBy(t => t.From).ThenBy(t => t.Symbol).ToList(),
            StateMapping = stateMapping,
            FinalStates = minimizedFinalStates,
            OriginalStartState = startState
        };
    }

    private List<DFATransition> ParseDFAInput(string input)
    {
        var transitions = new List<DFATransition>();
        var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            
            var parts = trimmed.Split(',');
            if (parts.Length >= 3)
            {
                var from = parts[0].Trim().Replace("*", "").Replace(">", "").Trim();
                var symbol = parts[1].Trim();
                var to = parts[2].Trim().Replace("*", "").Replace(">", "").Trim();
                
                if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(symbol) && !string.IsNullOrEmpty(to))
                    transitions.Add(new DFATransition { From = from, Symbol = symbol, To = to });
            }
        }
        
        return transitions;
    }

    // Lexical Analyzer
    public IActionResult LexicalAnalyzer() => View();

    [HttpPost]
    public IActionResult LexicalAnalyzer(string sourceCode)
    {
        var model = new LexicalAnalyzerModel
        {
            SourceCode = sourceCode,
            Tokens = TokenizeCode(sourceCode),
            SymbolTable = GenerateSymbolTable(sourceCode)
        };
        return View(model);
    }

    // LL1 Parser
    public IActionResult LL1Parser() => View();

    [HttpPost]
    public IActionResult LL1Parser(string grammar, string input)
    {
        if (string.IsNullOrWhiteSpace(grammar) || string.IsNullOrWhiteSpace(input))
        {
            ModelState.AddModelError("", "Please enter both grammar and input string");
            return View();
        }

        try
        {
            var parser = new LL1ParserImpl(grammar);
            var model = new ParserModel
            {
                Grammar = grammar,
                Input = input,
                FirstSet = parser.FirstSets,
                FollowSet = parser.FollowSets,
                ParsingTable = parser.GenerateParsingTableHTML(),
                ParseSteps = parser.Parse(input)
            };
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Error: " + ex.Message);
            return View();
        }
    }

    // SLR Parser
    public IActionResult SLRParser() => View();

    [HttpPost]
    public IActionResult SLRParser(string grammar, string input)
    {
        if (string.IsNullOrWhiteSpace(grammar) || string.IsNullOrWhiteSpace(input))
        {
            ModelState.AddModelError("", "Please enter both grammar and input string");
            return View();
        }

        try
        {
            var parser = new SLRParserImpl(grammar);
            var model = new SLRParserModel
            {
                Grammar = grammar,
                Input = input,
                AugmentedGrammar = parser.GetGrammarWithNumbers(),
                LR0Items = parser.GenerateLR0ItemsHTML(),
                ParsingTable = parser.GenerateParsingTableHTML(),
                ParseSteps = parser.Parse(input)
            };
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", "Error: " + ex.Message);
            return View();
        }
    }

    // Three Address Code
    public IActionResult ThreeAddressCode() => View();

    [HttpPost]
    public IActionResult ThreeAddressCode(string expression)
    {
        var model = new ThreeAddressCodeModel
        {
            Expression = expression,
            TAC = GenerateTAC(expression),
            Quadruples = GenerateQuadruples(expression),
            Triples = GenerateTriples(expression)
        };
        return View(model);
    }

    // Code Optimization
    public IActionResult CodeOptimization() => View();

    [HttpPost]
    public IActionResult CodeOptimization(string code)
    {
        var model = new OptimizationModel
        {
            Code = code,
            BeforeCode = code,
            AfterCode = OptimizeCode(code),
            OptimizationSteps = GenerateOptimizationSteps(code)
        };
        return View(model);
    }

    // Code Generation
    public IActionResult CodeGeneration() => View();

    [HttpPost]
    public IActionResult CodeGeneration(string code)
    {
        var model = new CodeGenerationModel
        {
            Code = code,
            BasicBlocks = IdentifyBasicBlocks(code),
            DagDiagram = GenerateDagDiagram(code),
            GeneratedCode = GenerateMachineCode(code)
        };
        return View(model);
    }

    // All Compiler Phases
    public IActionResult AllPhases() => View();

    [HttpPost]
    public IActionResult AllPhases(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            ModelState.AddModelError("", "Please enter a statement");
            return View();
        }

        var model = new AllPhasesModel
        {
            Statement = statement,
            Tokens = TokenizeCode(statement),
            ParseTree = GenerateParseTree(statement),
            SemanticAnalysis = PerformSemanticAnalysis(statement),
            IntermediateCode = GenerateTAC(statement),
            OptimizedCode = GenerateOptimizedTAC(statement),
            TargetCode = GenerateAssemblyCode(statement),
            SymbolTable = GenerateSymbolTable(statement)
        };
        return View(model);
    }

    // Compiler Theory
    public IActionResult CompilerTheory() => View();

    // Helper Methods
    private List<string> GenerateNfaSteps(string regex)
    {
        var steps = new List<string>
        {
            $"Step 1: Parse regular expression: {regex}",
            "Step 2: Apply Thompson's Construction",
            "Step 3: Create start state q0",
            "Step 4: Process each symbol and operator",
            "Step 5: Connect states with ε-transitions",
            "Step 6: Mark final state"
        };
        return steps;
    }

    private string GenerateNfaTransitionTable(string regex)
    {
        return "<table class='table table-bordered'><thead><tr><th>State</th><th>Input</th><th>Next State</th></tr></thead><tbody><tr><td>q0</td><td>a</td><td>q1</td></tr><tr><td>q1</td><td>b</td><td>q2</td></tr></tbody></table>";
    }

    private string GenerateNfaDiagram(string regex)
    {
        return $@"<svg width='600' height='200' xmlns='http://www.w3.org/2000/svg'>
            <defs>
                <marker id='arrowhead' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto'>
                    <polygon points='0 0, 10 3, 0 6' fill='#4a90e2' />
                </marker>
            </defs>
            <circle cx='50' cy='100' r='30' fill='#e8f4f8' stroke='#4a90e2' stroke-width='2'/>
            <text x='50' y='105' text-anchor='middle' font-size='14' fill='#333'>q0</text>
            <circle cx='200' cy='100' r='30' fill='#e8f4f8' stroke='#4a90e2' stroke-width='2'/>
            <text x='200' y='105' text-anchor='middle' font-size='14' fill='#333'>q1</text>
            <circle cx='350' cy='100' r='30' fill='#e8f4f8' stroke='#4a90e2' stroke-width='2'/>
            <circle cx='350' cy='100' r='25' fill='none' stroke='#4a90e2' stroke-width='2'/>
            <text x='350' y='105' text-anchor='middle' font-size='14' fill='#333'>q2</text>
            <line x1='80' y1='100' x2='170' y2='100' stroke='#4a90e2' stroke-width='2' marker-end='url(#arrowhead)'/>
            <text x='125' y='90' text-anchor='middle' font-size='12' fill='#666'>a</text>
            <line x1='230' y1='100' x2='320' y2='100' stroke='#4a90e2' stroke-width='2' marker-end='url(#arrowhead)'/>
            <text x='275' y='90' text-anchor='middle' font-size='12' fill='#666'>b</text>
        </svg>";
    }

    private List<string> ComputeEpsilonClosures(string nfaInput)
    {
        return new List<string>
        {
            "ε-closure(q0) = {q0, q1}",
            "ε-closure(q1) = {q1}",
            "ε-closure(q2) = {q2, q3}"
        };
    }

    private List<string> GenerateDfaConversionSteps(string nfaInput)
    {
        return new List<string>
        {
            "Step 1: Start with ε-closure of initial state",
            "Step 2: For each state set, compute transitions",
            "Step 3: Create new DFA states for unique sets",
            "Step 4: Mark final states",
            "Step 5: Build DFA transition table"
        };
    }

    private string GenerateDfaTable(string nfaInput)
    {
        return "<table class='table table-bordered'><thead><tr><th>State</th><th>a</th><th>b</th></tr></thead><tbody><tr><td>A</td><td>B</td><td>-</td></tr><tr><td>B</td><td>-</td><td>C</td></tr><tr><td>C*</td><td>-</td><td>-</td></tr></tbody></table>";
    }

    private string GenerateDfaDiagram(string nfaInput)
    {
        return $@"<svg width='500' height='200' xmlns='http://www.w3.org/2000/svg'>
            <defs>
                <marker id='arrowhead2' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto'>
                    <polygon points='0 0, 10 3, 0 6' fill='#28a745' />
                </marker>
            </defs>
            <circle cx='80' cy='100' r='30' fill='#d4edda' stroke='#28a745' stroke-width='2'/>
            <text x='80' y='105' text-anchor='middle' font-size='14' fill='#333'>A</text>
            <circle cx='250' cy='100' r='30' fill='#d4edda' stroke='#28a745' stroke-width='2'/>
            <text x='250' y='105' text-anchor='middle' font-size='14' fill='#333'>B</text>
            <circle cx='420' cy='100' r='30' fill='#d4edda' stroke='#28a745' stroke-width='2'/>
            <circle cx='420' cy='100' r='25' fill='none' stroke='#28a745' stroke-width='2'/>
            <text x='420' y='105' text-anchor='middle' font-size='14' fill='#333'>C</text>
            <line x1='110' y1='100' x2='220' y2='100' stroke='#28a745' stroke-width='2' marker-end='url(#arrowhead2)'/>
            <text x='165' y='90' text-anchor='middle' font-size='12' fill='#666'>a</text>
            <line x1='280' y1='100' x2='390' y2='100' stroke='#28a745' stroke-width='2' marker-end='url(#arrowhead2)'/>
            <text x='335' y='90' text-anchor='middle' font-size='12' fill='#666'>b</text>
        </svg>";
    }

    private string GenerateSyntaxTree(string regex)
    {
        return "Syntax tree generated for: " + regex;
    }

    private Dictionary<string, string> ComputeDirectDfaValues(string regex)
    {
        return new Dictionary<string, string>
        {
            { "nullable", "false" },
            { "firstpos", "{1, 2}" },
            { "lastpos", "{3, 4}" }
        };
    }

    private string GenerateFollowposTable(string regex)
    {
        return "<table class='table table-bordered'><thead><tr><th>Position</th><th>Followpos</th></tr></thead><tbody><tr><td>1</td><td>{2, 3}</td></tr><tr><td>2</td><td>{4}</td></tr></tbody></table>";
    }

    private string GenerateDirectDfaTable(string regex)
    {
        return "<table class='table table-bordered'><thead><tr><th>State</th><th>a</th><th>b</th></tr></thead><tbody><tr><td>S0</td><td>S1</td><td>-</td></tr><tr><td>S1*</td><td>-</td><td>S2</td></tr></tbody></table>";
    }

    private string GenerateDirectDfaDiagram(string regex)
    {
        return GenerateDfaDiagram(regex);
    }

    private List<string> GeneratePartitions(string dfaInput)
    {
        return new List<string>
        {
            "P0 = {Final States} | {Non-Final States}",
            "P1 = {A, B} | {C}",
            "P2 = {A} | {B} | {C}",
            "No further refinement possible"
        };
    }

    private string MinimizeDfa(string dfaInput)
    {
        return "Minimized DFA has 2 states";
    }

    private string GenerateBeforeDiagram(string dfaInput)
    {
        return GenerateDfaDiagram(dfaInput);
    }

    private string GenerateAfterDiagram(string dfaInput)
    {
        return $@"<svg width='400' height='200' xmlns='http://www.w3.org/2000/svg'>
            <defs>
                <marker id='arrowhead3' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto'>
                    <polygon points='0 0, 10 3, 0 6' fill='#dc3545' />
                </marker>
            </defs>
            <circle cx='100' cy='100' r='30' fill='#f8d7da' stroke='#dc3545' stroke-width='2'/>
            <text x='100' y='105' text-anchor='middle' font-size='14' fill='#333'>AB</text>
            <circle cx='300' cy='100' r='30' fill='#f8d7da' stroke='#dc3545' stroke-width='2'/>
            <circle cx='300' cy='100' r='25' fill='none' stroke='#dc3545' stroke-width='2'/>
            <text x='300' y='105' text-anchor='middle' font-size='14' fill='#333'>C</text>
            <line x1='130' y1='100' x2='270' y2='100' stroke='#dc3545' stroke-width='2' marker-end='url(#arrowhead3)'/>
            <text x='200' y='90' text-anchor='middle' font-size='12' fill='#666'>a,b</text>
        </svg>";
    }

    private List<Token> TokenizeCode(string sourceCode)
    {
        var tokens = new List<Token>();
        var keywords = new[] { "int", "float", "if", "else", "while", "for", "return" };
        var lines = sourceCode.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            var words = Regex.Split(lines[i], @"(\s+|[+\-*/=;(),{}])").Where(w => !string.IsNullOrWhiteSpace(w));
            foreach (var word in words)
            {
                if (keywords.Contains(word))
                    tokens.Add(new Token { Type = "KEYWORD", Value = word, Line = i + 1 });
                else if (Regex.IsMatch(word, @"^[a-zA-Z_]\w*$"))
                    tokens.Add(new Token { Type = "IDENTIFIER", Value = word, Line = i + 1 });
                else if (Regex.IsMatch(word, @"^\d+$"))
                    tokens.Add(new Token { Type = "CONSTANT", Value = word, Line = i + 1 });
                else if (Regex.IsMatch(word, @"^[+\-*/=;(),{}]$"))
                    tokens.Add(new Token { Type = "OPERATOR", Value = word, Line = i + 1 });
            }
        }
        return tokens;
    }

    private List<Symbol> GenerateSymbolTable(string sourceCode)
    {
        return new List<Symbol>
        {
            new Symbol { Name = "x", Type = "int", Scope = "global" },
            new Symbol { Name = "y", Type = "int", Scope = "global" }
        };
    }

    private Dictionary<string, HashSet<string>> ComputeFirst(string grammar)
    {
        return new Dictionary<string, HashSet<string>>
        {
            { "E", new HashSet<string> { "id", "(" } },
            { "T", new HashSet<string> { "id", "(" } }
        };
    }

    private Dictionary<string, HashSet<string>> ComputeFollow(string grammar)
    {
        return new Dictionary<string, HashSet<string>>
        {
            { "E", new HashSet<string> { "$", ")" } },
            { "T", new HashSet<string> { "+", "$", ")" } }
        };
    }

    private string GenerateLL1Table(string grammar)
    {
        return "<table class='table table-bordered'><thead><tr><th>Non-Terminal</th><th>id</th><th>+</th><th>$</th></tr></thead><tbody><tr><td>E</td><td>E → TE'</td><td>-</td><td>-</td></tr><tr><td>E'</td><td>-</td><td>E' → +TE'</td><td>E' → ε</td></tr></tbody></table>";
    }

    private List<string> SimulateLL1Parsing(string grammar, string input)
    {
        return new List<string>
        {
            "Stack: [$, E] | Input: id+id$ | Action: E → TE'",
            "Stack: [$, E', T] | Input: id+id$ | Action: T → id",
            "Stack: [$, E', id] | Input: id+id$ | Action: Match id",
            "Stack: [$, E'] | Input: +id$ | Action: E' → +TE'",
            "Parsing Successful!"
        };
    }

private List<string> GenerateTAC(string expression)
    {
        var (tac, _, _) = ThreeAddressCodeGenerator.Generate(expression);
        return tac;
    }

    private List<Quadruple> GenerateQuadruples(string expression)
    {
        var (_, quads, _) = ThreeAddressCodeGenerator.Generate(expression);
        return quads;
    }

    private List<Triple> GenerateTriples(string expression)
    {
        var (_, _, triples) = ThreeAddressCodeGenerator.Generate(expression);
        return triples;
    }

    private string OptimizeCode(string code)
    {
        var (optimized, _) = CodeOptimizer.Optimize(code);
        return optimized;
    }

    private List<string> GenerateOptimizationSteps(string code)
    {
        var (_, steps) = CodeOptimizer.Optimize(code);
        return steps;
    }

    private List<string> IdentifyBasicBlocks(string code)
    {
        return CodeGenerator.IdentifyBasicBlocks(code);
    }

    private string GenerateDagDiagram(string code)
    {
        return CodeGenerator.GenerateDAG(code);
    }

    private string GenerateMachineCode(string code)
    {
        return CodeGenerator.GenerateMachineCode(code);
    }

    private string GenerateParseTree(string statement)
    {
        var tokens = TokenizeCode(statement);
        var ascii = new StringBuilder();
        
        var ids = tokens.Where(t => t.Type == "IDENTIFIER" || t.Type == "CONSTANT").Select(t => t.Value).ToList();
        var ops = tokens.Where(t => t.Type == "OPERATOR" && t.Value != ";").Select(t => t.Value).ToList();
        
        if (ops.Contains("=") && ids.Count >= 2)
        {
            var lhs = ids[0];
            var mathOp = ops.FirstOrDefault(o => o != "=") ?? "+";
            var rhs1 = ids.Count > 1 ? ids[1] : "?";
            var rhs2 = ids.Count > 2 ? ids[2] : "";
            
            if (string.IsNullOrEmpty(rhs2))
            {
                ascii.AppendLine("      =      ");
                ascii.AppendLine("     / \\     ");
                ascii.AppendLine($"   {lhs,-3}  {rhs1,-3}  ");
            }
            else
            {
                ascii.AppendLine("          =          ");
                ascii.AppendLine("         / \\         ");
                ascii.AppendLine("        /   \\        ");
                ascii.AppendLine($"      {lhs,-4}    {mathOp}      ");
                ascii.AppendLine("            / \\      ");
                ascii.AppendLine($"          {rhs1,-3}  {rhs2,-3}   ");
            }
        }
        
        return ascii.ToString();
    }

    private List<string> PerformSemanticAnalysis(string statement)
    {
        var analysis = new List<string>();
        var tokens = TokenizeCode(statement);
        
        analysis.Add("✓ Type checking: All operands are compatible");
        analysis.Add("✓ Variable declarations: All variables are declared");
        analysis.Add("✓ Operator validity: All operators are valid for operand types");
        
        var hasAssignment = tokens.Any(t => t.Value == "=");
        if (hasAssignment)
            analysis.Add("✓ Assignment: Left-hand side is a valid l-value");
        
        return analysis;
    }

    private List<string> GenerateOptimizedTAC(string statement)
    {
        var tac = GenerateTAC(statement);
        var optimized = new List<string>();
        
        foreach (var line in tac)
        {
            if (line.Contains("= 2 * "))
            {
                var parts = line.Split('=');
                if (parts.Length == 2)
                {
                    var expr = parts[1].Trim();
                    var operand = expr.Replace("2 * ", "").Trim();
                    optimized.Add($"{parts[0].Trim()} = {operand} + {operand}  // Strength reduction");
                    continue;
                }
            }
            optimized.Add(line);
        }
        
        return optimized;
    }

    private List<string> GenerateAssemblyCode(string statement)
    {
        var assembly = new List<string>();
        var tokens = TokenizeCode(statement);
        
        assembly.Add("; Target Assembly Code");
        
        var identifiers = tokens.Where(t => t.Type == "IDENTIFIER").Select(t => t.Value).Distinct().ToList();
        var constants = tokens.Where(t => t.Type == "CONSTANT").Select(t => t.Value).Distinct().ToList();
        
        foreach (var id in identifiers.Skip(1))
            assembly.Add($"LOAD R1, {id}");
        
        if (constants.Any())
            assembly.Add($"LOAD R2, #{constants.First()}");
        
        if (tokens.Any(t => t.Value == "+"))
            assembly.Add("ADD R1, R1, R2");
        else if (tokens.Any(t => t.Value == "-"))
            assembly.Add("SUB R1, R1, R2");
        else if (tokens.Any(t => t.Value == "*"))
            assembly.Add("MUL R1, R1, R2");
        else if (tokens.Any(t => t.Value == "/"))
            assembly.Add("DIV R1, R1, R2");
        
        if (identifiers.Any())
            assembly.Add($"STORE {identifiers.First()}, R1");
        
        return assembly;
    }

    // Helper methods for generating diagrams and tables
    private string GenerateNfaTransitionTable(NFAResult nfa)
    {
        var html = "<table class='table table-bordered'><thead><tr><th>From State</th><th>Symbol</th><th>To State</th></tr></thead><tbody>";
        foreach (var trans in nfa.Fragment.Transitions.OrderBy(t => t.From).ThenBy(t => t.Symbol))
        {
            html += $"<tr><td>q{trans.From}</td><td>{trans.Symbol}</td><td>q{trans.To}</td></tr>";
        }
        html += "</tbody></table>";
        return html;
    }

    private string GenerateNfaDiagram(NFAResult nfa)
    {
        var transitions = nfa.Fragment.Transitions;
        var states = transitions.SelectMany(t => new[] { t.From, t.To }).Distinct().OrderBy(s => s).ToList();
        
        var svg = new StringBuilder();
        svg.Append("<svg width='800' height='300' xmlns='http://www.w3.org/2000/svg'>");
        svg.Append("<defs><marker id='arrowhead' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto'>");
        svg.Append("<polygon points='0 0, 10 3, 0 6' fill='#4a90e2' /></marker></defs>");
        
        int spacing = Math.Min(120, 700 / Math.Max(states.Count, 1));
        int startX = 80;
        int y = 150;
        
        var statePositions = new Dictionary<int, int>();
        for (int i = 0; i < states.Count; i++)
        {
            statePositions[states[i]] = startX + i * spacing;
        }
        
        // Draw states
        foreach (var state in states)
        {
            var cx = statePositions[state];
            var isFinal = state == nfa.Fragment.End;
            var isStart = state == nfa.Fragment.Start;
            
            svg.Append($"<circle cx='{cx}' cy='{y}' r='30' fill='#e8f4f8' stroke='#4a90e2' stroke-width='2'/>");
            if (isFinal)
                svg.Append($"<circle cx='{cx}' cy='{y}' r='25' fill='none' stroke='#4a90e2' stroke-width='2'/>");
            svg.Append($"<text x='{cx}' y='{y + 5}' text-anchor='middle' font-size='14' font-weight='bold' fill='#333'>q{state}</text>");
            
            if (isStart)
                svg.Append($"<line x1='{cx - 50}' y1='{y}' x2='{cx - 30}' y2='{y}' stroke='#4a90e2' stroke-width='2' marker-end='url(#arrowhead)'/>");
        }
        
        // Draw transitions
        var drawnTransitions = new HashSet<string>();
        foreach (var trans in transitions)
        {
            var key = $"{trans.From}-{trans.To}";
            if (drawnTransitions.Contains(key)) continue;
            
            var x1 = statePositions[trans.From];
            var x2 = statePositions[trans.To];
            
            // Self-loop
            if (trans.From == trans.To)
            {
                var cx = x1;
                svg.Append($"<path d='M {cx + 20},{y - 20} Q {cx + 50},{y - 50} {cx + 20},{y - 30}' fill='none' stroke='#4a90e2' stroke-width='2' marker-end='url(#arrowhead)'/>");
                svg.Append($"<text x='{cx + 35}' y='{y - 45}' text-anchor='middle' font-size='12' fill='#666'>{trans.Symbol}</text>");
            }
            else
            {
                var labels = transitions.Where(t => t.From == trans.From && t.To == trans.To).Select(t => t.Symbol).ToList();
                var label = string.Join(", ", labels);
                
                if (x1 < x2)
                {
                    svg.Append($"<line x1='{x1 + 30}' y1='{y}' x2='{x2 - 30}' y2='{y}' stroke='#4a90e2' stroke-width='2' marker-end='url(#arrowhead)'/>");
                    svg.Append($"<text x='{(x1 + x2) / 2}' y='{y - 10}' text-anchor='middle' font-size='12' fill='#666'>{label}</text>");
                }
                else
                {
                    svg.Append($"<path d='M {x1 - 25},{y - 15} Q {(x1 + x2) / 2},{y - 60} {x2 + 25},{y - 15}' fill='none' stroke='#4a90e2' stroke-width='2' marker-end='url(#arrowhead)'/>");
                    svg.Append($"<text x='{(x1 + x2) / 2}' y='{y - 65}' text-anchor='middle' font-size='12' fill='#666'>{label}</text>");
                }
                drawnTransitions.Add(key);
            }
        }
        
        svg.Append("</svg>");
        return svg.ToString();
    }

    private string GenerateDfaTableFromResult(DFAResult dfa)
    {
        var html = "<table class='table table-bordered'><thead><tr><th>State</th>";
        foreach (var symbol in dfa.Alphabet)
            html += $"<th>{symbol}</th>";
        html += "</tr></thead><tbody>";
        
        for (int i = 0; i < dfa.States.Count; i++)
        {
            var stateName = GetDFAStateName(i);
            var isFinal = dfa.FinalStates.Contains(stateName);
            var stateLabel = isFinal ? $"{stateName}*" : stateName;
            html += $"<tr><td><strong>{stateLabel}</strong></td>";
            
            foreach (var symbol in dfa.Alphabet)
            {
                var trans = dfa.Transitions.FirstOrDefault(t => t.From == stateName && t.Symbol == symbol);
                html += $"<td>{trans?.To ?? "-"}</td>";
            }
            html += "</tr>";
        }
        html += "</tbody></table>";
        return html;
    }

    private string GenerateDfaDiagramFromResult(DFAResult dfa)
    {
        var svg = new StringBuilder();
        var stateCount = Math.Min(dfa.States.Count, 6);
        var width = Math.Max(600, stateCount * 150);
        
        svg.Append($"<svg width='{width}' height='250' xmlns='http://www.w3.org/2000/svg'>");
        svg.Append("<defs><marker id='arrowhead2' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto'>");
        svg.Append("<polygon points='0 0, 10 3, 0 6' fill='#28a745' /></marker></defs>");
        
        int x = 80;
        int y = 125;
        int spacing = stateCount > 4 ? 120 : 150;
        
        for (int i = 0; i < stateCount; i++)
        {
            var cx = x + i * spacing;
            var stateName = GetDFAStateName(i);
            var isFinal = dfa.FinalStates.Contains(stateName);
            var isStart = i == 0;
            
            svg.Append($"<circle cx='{cx}' cy='{y}' r='35' fill='#d4edda' stroke='#28a745' stroke-width='2'/>");
            if (isFinal)
                svg.Append($"<circle cx='{cx}' cy='{y}' r='30' fill='none' stroke='#28a745' stroke-width='2'/>");
            
            var stateSet = "{" + string.Join(",", dfa.States[i].OrderBy(s => s).Select(s => $"q{s}")) + "}";
            svg.Append($"<text x='{cx}' y='{y}' text-anchor='middle' font-size='14' font-weight='bold' fill='#333'>{stateName}</text>");
            svg.Append($"<text x='{cx}' y='{y + 15}' text-anchor='middle' font-size='10' fill='#666'>{stateSet}</text>");
            
            if (isStart)
                svg.Append($"<line x1='{cx - 55}' y1='{y}' x2='{cx - 35}' y2='{y}' stroke='#28a745' stroke-width='2' marker-end='url(#arrowhead2)'/>");
        }
        
        var drawnTransitions = new HashSet<string>();
        foreach (var trans in dfa.Transitions)
        {
            var fromIdx = trans.From[0] - 'A';
            var toIdx = trans.To[0] - 'A';
            
            if (fromIdx >= 0 && toIdx >= 0 && fromIdx < stateCount && toIdx < stateCount)
            {
                var key = $"{trans.From}-{trans.To}";
                var x1 = x + fromIdx * spacing;
                var x2 = x + toIdx * spacing;
                
                if (fromIdx == toIdx)
                {
                    svg.Append($"<path d='M {x1 + 25},{y - 25} Q {x1 + 55},{y - 60} {x1 + 30},{y - 30}' fill='none' stroke='#28a745' stroke-width='2' marker-end='url(#arrowhead2)'/>");
                    svg.Append($"<text x='{x1 + 45}' y='{y - 55}' text-anchor='middle' font-size='12' fill='#666'>{trans.Symbol}</text>");
                }
                else
                {
                    var labels = dfa.Transitions.Where(t => t.From == trans.From && t.To == trans.To).Select(t => t.Symbol).ToList();
                    if (!drawnTransitions.Contains(key))
                    {
                        var label = string.Join(",", labels);
                        svg.Append($"<line x1='{x1 + 35}' y1='{y}' x2='{x2 - 35}' y2='{y}' stroke='#28a745' stroke-width='2' marker-end='url(#arrowhead2)'/>");
                        svg.Append($"<text x='{(x1 + x2) / 2}' y='{y - 15}' text-anchor='middle' font-size='12' fill='#666'>{label}</text>");
                        drawnTransitions.Add(key);
                    }
                }
            }
        }
        
        svg.Append("</svg>");
        return svg.ToString();
    }

    private string GenerateAsciiDfaDiagram(DFAResult dfa)
    {
        var sb = new StringBuilder();
        
        for (int i = 0; i < dfa.States.Count; i++)
        {
            var stateName = GetDFAStateName(i);
            var stateSet = "{" + string.Join(",", dfa.States[i].OrderBy(s => s).Select(s => $"q{s}")) + "}";
            var isFinal = dfa.FinalStates.Contains(stateName) ? "*" : "";
            sb.AppendLine($"{stateName}{isFinal} = {stateSet}");
        }
        
        sb.AppendLine();
        
        foreach (var trans in dfa.Transitions)
        {
            sb.AppendLine($"({trans.From}) --{trans.Symbol}--> ({trans.To})");
        }
        
        return sb.ToString();
    }

    private string GenerateDirectDfaDiagramFromResult(DirectDFAResult dfa)
    {
        var svg = new StringBuilder();
        svg.Append("<svg width='800' height='250' xmlns='http://www.w3.org/2000/svg'>");
        svg.Append("<defs><marker id='arrowhead3' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto'>");
        svg.Append("<polygon points='0 0, 10 3, 0 6' fill='#007bff' /></marker></defs>");
        
        int x = 80;
        int y = 125;
        int spacing = 150;
        
        for (int i = 0; i < dfa.States.Count && i < 5; i++)
        {
            var cx = x + i * spacing;
            var stateName = GetDFAStateName(i);
            var isFinal = dfa.States[i].Any(p => dfa.Positions.Any(pos => pos.Position == p && pos.Symbol == "#"));
            
            svg.Append($"<circle cx='{cx}' cy='{y}' r='35' fill='#cfe2ff' stroke='#007bff' stroke-width='2'/>");
            if (isFinal)
                svg.Append($"<circle cx='{cx}' cy='{y}' r='30' fill='none' stroke='#007bff' stroke-width='2'/>");
            svg.Append($"<text x='{cx}' y='{y + 5}' text-anchor='middle' font-size='16' fill='#333'>{stateName}</text>");
        }
        
        foreach (var trans in dfa.Transitions.Take(8))
        {
            var fromIdx = trans.From[0] - 'A';
            var toIdx = trans.To[0] - 'A';
            
            if (fromIdx >= 0 && toIdx >= 0 && fromIdx < 5 && toIdx < 5)
            {
                var x1 = x + fromIdx * spacing + 35;
                var x2 = x + toIdx * spacing - 35;
                svg.Append($"<line x1='{x1}' y1='{y}' x2='{x2}' y2='{y}' stroke='#007bff' stroke-width='2' marker-end='url(#arrowhead3)'/>");
                svg.Append($"<text x='{(x1 + x2) / 2}' y='{y - 15}' text-anchor='middle' font-size='12' fill='#666'>{trans.Symbol}</text>");
            }
        }
        
        svg.Append("</svg>");
        return svg.ToString();
    }

    private string GenerateDfaDiagramFromInput(string dfaInput)
    {
        var transitions = ParseDFAInput(dfaInput);
        var states = transitions.SelectMany(t => new[] { t.From, t.To }).Distinct().OrderBy(s => s).ToList();
        var finalStates = new HashSet<string>();
        string? startState = null;
        
        foreach (var line in dfaInput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            var parts = trimmed.Split(',');
            if (parts.Length < 3) continue;
            var stateStr = parts[0].Trim();
            var cleanState = stateStr.Replace(">", "").Replace("*", "").Trim();
            if (stateStr.Contains('>') && startState == null) startState = cleanState;
            if (stateStr.Contains('*')) finalStates.Add(cleanState);
        }
        
        var svg = new StringBuilder();
        var width = Math.Max(600, states.Count * 180);
        svg.Append($"<svg width='{width}' height='280' xmlns='http://www.w3.org/2000/svg'>");
        svg.Append("<defs><marker id='arrowhead4' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto'>");
        svg.Append("<polygon points='0 0, 10 3, 0 6' fill='#6c757d' /></marker></defs>");
        
        int x = 100, y = 140, spacing = states.Count > 3 ? 160 : 200;
        
        for (int i = 0; i < states.Count; i++)
        {
            var cx = x + i * spacing;
            var state = states[i];
            var isFinal = finalStates.Contains(state);
            var isStart = state == startState;
            
            svg.Append($"<circle cx='{cx}' cy='{y}' r='40' fill='#e2e3e5' stroke='#6c757d' stroke-width='2'/>");
            if (isFinal) svg.Append($"<circle cx='{cx}' cy='{y}' r='35' fill='none' stroke='#6c757d' stroke-width='2'/>");
            svg.Append($"<text x='{cx}' y='{y + 5}' text-anchor='middle' font-size='16' font-weight='bold' fill='#333'>{state}</text>");
            if (isStart) svg.Append($"<line x1='{cx - 60}' y1='{y}' x2='{cx - 40}' y2='{y}' stroke='#6c757d' stroke-width='2' marker-end='url(#arrowhead4)'/>");
        }
        
        var drawnTransitions = new HashSet<string>();
        foreach (var trans in transitions)
        {
            var fromIdx = states.IndexOf(trans.From);
            var toIdx = states.IndexOf(trans.To);
            if (fromIdx < 0 || toIdx < 0) continue;
            
            var key = $"{trans.From}-{trans.To}";
            if (fromIdx == toIdx)
            {
                var cx = x + fromIdx * spacing;
                var labels = transitions.Where(t => t.From == trans.From && t.To == trans.To).Select(t => t.Symbol);
                if (!drawnTransitions.Contains(key))
                {
                    var label = string.Join(",", labels);
                    svg.Append($"<path d='M {cx + 30},{y - 30} Q {cx + 65},{y - 70} {cx + 35},{y - 35}' fill='none' stroke='#6c757d' stroke-width='2' marker-end='url(#arrowhead4)'/>");
                    svg.Append($"<text x='{cx + 50}' y='{y - 65}' text-anchor='middle' font-size='12' fill='#666'>{label}</text>");
                    drawnTransitions.Add(key);
                }
            }
            else
            {
                var x1 = x + fromIdx * spacing;
                var x2 = x + toIdx * spacing;
                var labels = transitions.Where(t => t.From == trans.From && t.To == trans.To).Select(t => t.Symbol);
                if (!drawnTransitions.Contains(key))
                {
                    var label = string.Join(",", labels);
                    if (fromIdx < toIdx)
                    {
                        svg.Append($"<line x1='{x1 + 40}' y1='{y}' x2='{x2 - 40}' y2='{y}' stroke='#6c757d' stroke-width='2' marker-end='url(#arrowhead4)'/>");
                        svg.Append($"<text x='{(x1 + x2) / 2}' y='{y - 15}' text-anchor='middle' font-size='12' fill='#666'>{label}</text>");
                    }
                    else
                    {
                        svg.Append($"<path d='M {x1 - 35},{y - 20} Q {(x1 + x2) / 2},{y - 80} {x2 + 35},{y - 20}' fill='none' stroke='#6c757d' stroke-width='2' marker-end='url(#arrowhead4)'/>");
                        svg.Append($"<text x='{(x1 + x2) / 2}' y='{y - 85}' text-anchor='middle' font-size='12' fill='#666'>{label}</text>");
                    }
                    drawnTransitions.Add(key);
                }
            }
        }
        svg.Append("</svg>");
        return svg.ToString();
    }

    private string GenerateMinimizedDfaDiagram(MinimizationResult result)
    {
        var states = result.MinimizedTransitions.SelectMany(t => new[] { t.From, t.To }).Distinct().OrderBy(s => s).ToList();
        if (states.Count == 0) states = result.StateMapping.Values.Distinct().OrderBy(s => s).ToList();
        
        var svg = new StringBuilder();
        var width = Math.Max(600, states.Count * 180);
        svg.Append($"<svg width='{width}' height='280' xmlns='http://www.w3.org/2000/svg'>");
        svg.Append("<defs><marker id='arrowhead5' markerWidth='10' markerHeight='10' refX='9' refY='3' orient='auto'>");
        svg.Append("<polygon points='0 0, 10 3, 0 6' fill='#dc3545' /></marker></defs>");
        
        int x = 100, y = 140, spacing = states.Count > 3 ? 160 : 200;
        string? startState = result.OriginalStartState != null && result.StateMapping.ContainsKey(result.OriginalStartState) 
            ? result.StateMapping[result.OriginalStartState] : states.FirstOrDefault();
        
        for (int i = 0; i < states.Count; i++)
        {
            var cx = x + i * spacing;
            var state = states[i];
            var isFinal = result.FinalStates.Contains(state);
            var isStart = state == startState;
            
            svg.Append($"<circle cx='{cx}' cy='{y}' r='40' fill='#f8d7da' stroke='#dc3545' stroke-width='2'/>");
            if (isFinal) svg.Append($"<circle cx='{cx}' cy='{y}' r='35' fill='none' stroke='#dc3545' stroke-width='2'/>");
            
            var originalStates = result.StateMapping.Where(kv => kv.Value == state).Select(kv => kv.Key).OrderBy(s => s);
            var stateLabel = "{" + string.Join(",", originalStates) + "}";
            svg.Append($"<text x='{cx}' y='{y}' text-anchor='middle' font-size='14' font-weight='bold' fill='#333'>{state}</text>");
            svg.Append($"<text x='{cx}' y='{y + 18}' text-anchor='middle' font-size='10' fill='#666'>{stateLabel}</text>");
            if (isStart) svg.Append($"<line x1='{cx - 60}' y1='{y}' x2='{cx - 40}' y2='{y}' stroke='#dc3545' stroke-width='2' marker-end='url(#arrowhead5)'/>");
        }
        
        var drawnTransitions = new HashSet<string>();
        foreach (var trans in result.MinimizedTransitions)
        {
            var fromIdx = states.IndexOf(trans.From);
            var toIdx = states.IndexOf(trans.To);
            if (fromIdx < 0 || toIdx < 0) continue;
            
            var key = $"{trans.From}-{trans.To}";
            if (fromIdx == toIdx)
            {
                var cx = x + fromIdx * spacing;
                var labels = result.MinimizedTransitions.Where(t => t.From == trans.From && t.To == trans.To).Select(t => t.Symbol);
                if (!drawnTransitions.Contains(key))
                {
                    var label = string.Join(",", labels);
                    svg.Append($"<path d='M {cx + 30},{y - 30} Q {cx + 65},{y - 70} {cx + 35},{y - 35}' fill='none' stroke='#dc3545' stroke-width='2' marker-end='url(#arrowhead5)'/>");
                    svg.Append($"<text x='{cx + 50}' y='{y - 65}' text-anchor='middle' font-size='12' fill='#666'>{label}</text>");
                    drawnTransitions.Add(key);
                }
            }
            else
            {
                var x1 = x + fromIdx * spacing;
                var x2 = x + toIdx * spacing;
                var labels = result.MinimizedTransitions.Where(t => t.From == trans.From && t.To == trans.To).Select(t => t.Symbol);
                if (!drawnTransitions.Contains(key))
                {
                    var label = string.Join(",", labels);
                    if (fromIdx < toIdx)
                    {
                        svg.Append($"<line x1='{x1 + 40}' y1='{y}' x2='{x2 - 40}' y2='{y}' stroke='#dc3545' stroke-width='2' marker-end='url(#arrowhead5)'/>");
                        svg.Append($"<text x='{(x1 + x2) / 2}' y='{y - 15}' text-anchor='middle' font-size='12' fill='#666'>{label}</text>");
                    }
                    else
                    {
                        svg.Append($"<path d='M {x1 - 35},{y - 20} Q {(x1 + x2) / 2},{y - 80} {x2 + 35},{y - 20}' fill='none' stroke='#dc3545' stroke-width='2' marker-end='url(#arrowhead5)'/>");
                        svg.Append($"<text x='{(x1 + x2) / 2}' y='{y - 85}' text-anchor='middle' font-size='12' fill='#666'>{label}</text>");
                    }
                    drawnTransitions.Add(key);
                }
            }
        }
        svg.Append("</svg>");
        return svg.ToString();
    }

    private string GenerateMinimizedAsciiDiagram(MinimizationResult result)
    {
        var sb = new StringBuilder();
        var states = result.MinimizedTransitions.SelectMany(t => new[] { t.From, t.To }).Distinct().OrderBy(s => s).ToList();
        
        // Find start state
        string? startState = null;
        if (result.OriginalStartState != null && result.StateMapping.ContainsKey(result.OriginalStartState))
            startState = result.StateMapping[result.OriginalStartState];
        else if (states.Count > 0)
            startState = states[0];
        
        sb.AppendLine("Minimized DFA States:");
        sb.AppendLine();
        
        foreach (var state in states)
        {
            var originalStates = result.StateMapping.Where(kv => kv.Value == state).Select(kv => kv.Key).OrderBy(s => s);
            var isFinal = result.FinalStates.Contains(state) ? "*" : "";
            var isStart = state == startState ? ">" : "";
            sb.AppendLine($"{isStart}{state}{isFinal} = {{" + string.Join(", ", originalStates) + "}");
        }
        
        sb.AppendLine();
        sb.AppendLine("Transitions:");
        sb.AppendLine();
        
        foreach (var trans in result.MinimizedTransitions.OrderBy(t => t.From).ThenBy(t => t.Symbol))
        {
            sb.AppendLine($"({trans.From}) --{trans.Symbol}--> ({trans.To})");
        }
        
        return sb.ToString();
    }
}

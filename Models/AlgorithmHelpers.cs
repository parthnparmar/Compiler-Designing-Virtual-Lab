using System.Text;
using System.Text.RegularExpressions;

namespace Compiler_Designing_Virtual_Lab.Models;

public class LL1ParserImpl
{
    public Dictionary<string, HashSet<string>> FirstSets { get; private set; } = new();
    public Dictionary<string, HashSet<string>> FollowSets { get; private set; } = new();
    private Dictionary<string, List<string>> Productions = new();
    private HashSet<string> NonTerminals = new();
    private HashSet<string> Terminals = new();
    private string StartSymbol = "";
    private Dictionary<(string, string), string> ParsingTable = new();

    public LL1ParserImpl(string grammar)
    {
        ParseGrammar(grammar);
        ComputeFirstSets();
        ComputeFollowSets();
        BuildParsingTable();
    }

    private void ParseGrammar(string grammar)
    {
        var lines = grammar.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(new[] { "->" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var lhs = parts[0].Trim();
                var rhs = parts[1].Trim().Split('|').Select(p => p.Trim()).ToList();
                
                if (string.IsNullOrEmpty(StartSymbol))
                    StartSymbol = lhs;
                    
                NonTerminals.Add(lhs);
                if (!Productions.ContainsKey(lhs))
                    Productions[lhs] = new List<string>();
                Productions[lhs].AddRange(rhs);
            }
        }
        
        foreach (var prod in Productions.Values.SelectMany(p => p))
        {
            var symbols = prod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var symbol in symbols)
            {
                if (!NonTerminals.Contains(symbol) && symbol != "ε" && symbol != "epsilon")
                    Terminals.Add(symbol);
            }
        }
    }

    private void ComputeFirstSets()
    {
        foreach (var nt in NonTerminals)
            FirstSets[nt] = new HashSet<string>();

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var nt in NonTerminals)
            {
                foreach (var prod in Productions[nt])
                {
                    var symbols = prod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    
                    if (symbols.Length == 0 || symbols[0] == "ε" || symbols[0] == "epsilon")
                    {
                        if (FirstSets[nt].Add("ε"))
                            changed = true;
                    }
                    else
                    {
                        bool allNullable = true;
                        foreach (var symbol in symbols)
                        {
                            if (Terminals.Contains(symbol))
                            {
                                if (FirstSets[nt].Add(symbol))
                                    changed = true;
                                allNullable = false;
                                break;
                            }
                            else if (NonTerminals.Contains(symbol))
                            {
                                foreach (var f in FirstSets[symbol].Where(x => x != "ε"))
                                {
                                    if (FirstSets[nt].Add(f))
                                        changed = true;
                                }
                                if (!FirstSets[symbol].Contains("ε"))
                                {
                                    allNullable = false;
                                    break;
                                }
                            }
                        }
                        if (allNullable && FirstSets[nt].Add("ε"))
                            changed = true;
                    }
                }
            }
        }
    }

    private void ComputeFollowSets()
    {
        foreach (var nt in NonTerminals)
            FollowSets[nt] = new HashSet<string>();
        
        FollowSets[StartSymbol].Add("$");

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var nt in NonTerminals)
            {
                foreach (var prod in Productions[nt])
                {
                    var symbols = prod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < symbols.Length; i++)
                    {
                        if (NonTerminals.Contains(symbols[i]))
                        {
                            bool allNullable = true;
                            for (int j = i + 1; j < symbols.Length; j++)
                            {
                                if (Terminals.Contains(symbols[j]))
                                {
                                    if (FollowSets[symbols[i]].Add(symbols[j]))
                                        changed = true;
                                    allNullable = false;
                                    break;
                                }
                                else if (NonTerminals.Contains(symbols[j]))
                                {
                                    foreach (var f in FirstSets[symbols[j]].Where(x => x != "ε"))
                                    {
                                        if (FollowSets[symbols[i]].Add(f))
                                            changed = true;
                                    }
                                    if (!FirstSets[symbols[j]].Contains("ε"))
                                    {
                                        allNullable = false;
                                        break;
                                    }
                                }
                            }
                            if (allNullable)
                            {
                                foreach (var f in FollowSets[nt])
                                {
                                    if (FollowSets[symbols[i]].Add(f))
                                        changed = true;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void BuildParsingTable()
    {
        foreach (var nt in NonTerminals)
        {
            foreach (var prod in Productions[nt])
            {
                var first = GetFirstOfProduction(prod);
                
                foreach (var terminal in first.Where(x => x != "ε"))
                {
                    var key = (nt, terminal);
                    if (!ParsingTable.ContainsKey(key))
                        ParsingTable[key] = prod;
                }
                
                if (first.Contains("ε"))
                {
                    foreach (var follow in FollowSets[nt])
                    {
                        var key = (nt, follow);
                        if (!ParsingTable.ContainsKey(key))
                            ParsingTable[key] = prod;
                    }
                }
            }
        }
    }

    public string GenerateParsingTableHTML()
    {
        var html = "<table class='table table-bordered'><thead><tr><th>Non-Terminal</th>";
        var allTerminals = Terminals.OrderBy(x => x).ToList();
        foreach (var t in allTerminals)
            html += $"<th>{t}</th>";
        html += "<th>$</th></tr></thead><tbody>";

        foreach (var nt in NonTerminals.OrderBy(x => x))
        {
            html += $"<tr><td><strong>{nt}</strong></td>";
            foreach (var t in allTerminals)
            {
                var entry = ParsingTable.ContainsKey((nt, t)) ? $"{nt} → {ParsingTable[(nt, t)]}" : "-";
                html += $"<td>{entry}</td>";
            }
            var dollarEntry = ParsingTable.ContainsKey((nt, "$")) ? $"{nt} → {ParsingTable[(nt, "$")]}" : "-";
            html += $"<td>{dollarEntry}</td></tr>";
        }
        html += "</tbody></table>";
        return html;
    }

    private HashSet<string> GetFirstOfProduction(string prod)
    {
        var result = new HashSet<string>();
        var symbols = prod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (symbols.Length == 0 || symbols[0] == "ε" || symbols[0] == "epsilon")
        {
            result.Add("ε");
            return result;
        }

        foreach (var symbol in symbols)
        {
            if (Terminals.Contains(symbol))
            {
                result.Add(symbol);
                break;
            }
            else if (NonTerminals.Contains(symbol))
            {
                foreach (var f in FirstSets[symbol].Where(x => x != "ε"))
                    result.Add(f);
                if (!FirstSets[symbol].Contains("ε"))
                    break;
            }
        }
        
        bool allNullable = symbols.All(s => NonTerminals.Contains(s) && FirstSets[s].Contains("ε"));
        if (allNullable)
            result.Add("ε");
            
        return result;
    }

    public List<string> Parse(string input)
    {
        var steps = new List<string>();
        var stack = new Stack<string>();
        stack.Push("$");
        stack.Push(StartSymbol);
        
        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        tokens.Add("$");
        int index = 0;

        steps.Add("<table class='table table-bordered'><thead><tr><th>Stack</th><th>Input</th><th>Action</th></tr></thead><tbody>");

        while (stack.Count > 0)
        {
            var top = stack.Peek();
            var current = tokens[index];
            
            var stackStr = string.Join("", stack.Reverse());
            var inputStr = string.Join("", tokens.Skip(index));
            
            if (top == "$" && current == "$")
            {
                steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td><span class='badge bg-success'>Accept</span></td></tr>");
                break;
            }
            else if (Terminals.Contains(top) || top == "$")
            {
                if (top == current)
                {
                    steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td>Match {top}</td></tr>");
                    stack.Pop();
                    index++;
                }
                else
                {
                    steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td><span class='badge bg-danger'>Error</span></td></tr>");
                    break;
                }
            }
            else if (NonTerminals.Contains(top))
            {
                if (ParsingTable.ContainsKey((top, current)))
                {
                    var production = ParsingTable[(top, current)];
                    steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td>{top} → {production}</td></tr>");
                    stack.Pop();
                    var symbols = production.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (symbols.Length > 0 && symbols[0] != "ε" && symbols[0] != "epsilon")
                    {
                        for (int i = symbols.Length - 1; i >= 0; i--)
                            stack.Push(symbols[i]);
                    }
                }
                else
                {
                    steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td><span class='badge bg-danger'>Error</span></td></tr>");
                    break;
                }
            }
        }

        steps.Add("</tbody></table>");
        return steps;
    }
}


// NFA Helper Classes
public class NFAFragment
{
    public int Start { get; set; }
    public int End { get; set; }
    public List<NFATransition> Transitions { get; set; } = new();
}

public class NFATransition
{
    public int From { get; set; }
    public string Symbol { get; set; } = "";
    public int To { get; set; }
}

public class NFAResult
{
    public NFAFragment Fragment { get; set; } = new();
    public List<string> Steps { get; set; } = new();
}

public class DFATransition
{
    public string From { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string To { get; set; } = "";
}

public class DFAResult
{
    public List<HashSet<int>> States { get; set; } = new();
    public List<DFATransition> Transitions { get; set; } = new();
    public List<string> EpsilonClosures { get; set; } = new();
    public List<string> Steps { get; set; } = new();
    public List<string> Alphabet { get; set; } = new();
    public HashSet<string> FinalStates { get; set; } = new();
}

public class SyntaxTreeNode
{
    public string Symbol { get; set; } = "";
    public int Position { get; set; }
    public bool IsLeaf { get; set; }
    public bool Nullable { get; set; }
    public HashSet<int> Firstpos { get; set; } = new();
    public HashSet<int> Lastpos { get; set; } = new();
    public SyntaxTreeNode? Left { get; set; }
    public SyntaxTreeNode? Right { get; set; }
}

public class PositionInfo
{
    public int Position { get; set; }
    public string Symbol { get; set; } = "";
}

public class DirectDFAResult
{
    public string SyntaxTree { get; set; } = "";
    public Dictionary<string, string> ComputedValues { get; set; } = new();
    public string FollowposTable { get; set; } = "";
    public string DfaTable { get; set; } = "";
    public List<HashSet<int>> States { get; set; } = new();
    public List<DFATransition> Transitions { get; set; } = new();
    public List<string> Alphabet { get; set; } = new();
    public List<PositionInfo> Positions { get; set; } = new();
}

public class MinimizationResult
{
    public List<string> Partitions { get; set; } = new();
    public string Summary { get; set; } = "";
    public List<DFATransition> MinimizedTransitions { get; set; } = new();
    public Dictionary<string, string> StateMapping { get; set; } = new();
    public HashSet<string> FinalStates { get; set; } = new();
    public string? OriginalStartState { get; set; }
}

public static class ASCIIDiagramGenerator
{
    public static string GenerateSyntaxTreeDiagram(SyntaxTreeNode root)
    {
        var sb = new System.Text.StringBuilder();
        
        // Add detailed information
        sb.AppendLine("Syntax Tree with Positions:");
        sb.AppendLine();
        
        var lines = new List<string>();
        BuildTree(root, "", true, lines, 0);
        sb.AppendLine(string.Join("\n", lines));
        
        sb.AppendLine();
        sb.AppendLine("Node Details:");
        AddNodeDetails(root, sb);
        
        return sb.ToString();
    }

    private static void BuildTree(SyntaxTreeNode node, string prefix, bool isLast, List<string> lines, int depth)
    {
        if (node == null) return;

        var marker = depth == 0 ? "" : (isLast ? "└── " : "├── ");
        var display = node.IsLeaf 
            ? $"({node.Position}){node.Symbol}" 
            : node.Symbol;
        
        lines.Add(prefix + marker + display);

        var newPrefix = depth == 0 ? "" : prefix + (isLast ? "    " : "│   ");

        if (node.Left != null && node.Right != null)
        {
            BuildTree(node.Left, newPrefix, false, lines, depth + 1);
            BuildTree(node.Right, newPrefix, true, lines, depth + 1);
        }
        else if (node.Left != null)
        {
            BuildTree(node.Left, newPrefix, true, lines, depth + 1);
        }
    }
    
    private static void AddNodeDetails(SyntaxTreeNode node, System.Text.StringBuilder sb)
    {
        if (node == null) return;
        
        var nodeDesc = node.IsLeaf ? $"Leaf ({node.Position}){node.Symbol}" : $"Node {node.Symbol}";
        sb.AppendLine($"{nodeDesc}:");
        sb.AppendLine($"  nullable = {node.Nullable.ToString().ToLower()}");
        sb.AppendLine($"  firstpos = {{" + string.Join(", ", node.Firstpos.OrderBy(x => x)) + "}");
        sb.AppendLine($"  lastpos = {{" + string.Join(", ", node.Lastpos.OrderBy(x => x)) + "}");
        sb.AppendLine();
        
        if (node.Left != null) AddNodeDetails(node.Left, sb);
        if (node.Right != null) AddNodeDetails(node.Right, sb);
    }
}

// SLR Parser Implementation
public class SLRParserImpl
{
    private Dictionary<string, List<string>> Productions = new();
    private HashSet<string> NonTerminals = new();
    private HashSet<string> Terminals = new();
    private string StartSymbol = "";
    private string AugmentedStart = "";
    private List<LR0ItemSet> CanonicalCollection = new();
    private Dictionary<string, HashSet<string>> FollowSets = new();
    private Dictionary<(int, string), string> ActionTable = new();
    private Dictionary<(int, string), int> GotoTable = new();
    
    public SLRParserImpl(string grammar)
    {
        ParseGrammar(grammar);
        AugmentGrammar();
        ComputeFollowSets();
        BuildCanonicalCollection();
        BuildParsingTable();
    }
    
    private void ParseGrammar(string grammar)
    {
        var lines = grammar.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(new[] { "->" }, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                var lhs = parts[0].Trim();
                var rhs = parts[1].Trim().Split('|').Select(p => p.Trim()).ToList();
                
                if (string.IsNullOrEmpty(StartSymbol))
                    StartSymbol = lhs;
                    
                NonTerminals.Add(lhs);
                if (!Productions.ContainsKey(lhs))
                    Productions[lhs] = new List<string>();
                Productions[lhs].AddRange(rhs);
            }
        }
        
        foreach (var prod in Productions.Values.SelectMany(p => p))
        {
            var symbols = prod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var symbol in symbols)
            {
                if (!NonTerminals.Contains(symbol) && symbol != "ε" && symbol != "epsilon")
                    Terminals.Add(symbol);
            }
        }
    }
    
    private void AugmentGrammar()
    {
        AugmentedStart = StartSymbol + "'";
        NonTerminals.Add(AugmentedStart);
        Productions[AugmentedStart] = new List<string> { StartSymbol };
    }
    
    private void ComputeFollowSets()
    {
        FollowSets = new Dictionary<string, HashSet<string>>();
        foreach (var nt in NonTerminals)
            FollowSets[nt] = new HashSet<string>();
        
        FollowSets[StartSymbol].Add("$");
        
        var firstSets = ComputeFirstSets();
        
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var nt in NonTerminals)
            {
                foreach (var prod in Productions[nt])
                {
                    var symbols = prod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < symbols.Length; i++)
                    {
                        if (NonTerminals.Contains(symbols[i]))
                        {
                            bool allNullable = true;
                            for (int j = i + 1; j < symbols.Length; j++)
                            {
                                if (Terminals.Contains(symbols[j]))
                                {
                                    if (FollowSets[symbols[i]].Add(symbols[j]))
                                        changed = true;
                                    allNullable = false;
                                    break;
                                }
                                else if (NonTerminals.Contains(symbols[j]))
                                {
                                    foreach (var f in firstSets[symbols[j]].Where(x => x != "ε"))
                                    {
                                        if (FollowSets[symbols[i]].Add(f))
                                            changed = true;
                                    }
                                    if (!firstSets[symbols[j]].Contains("ε"))
                                    {
                                        allNullable = false;
                                        break;
                                    }
                                }
                            }
                            if (allNullable)
                            {
                                foreach (var f in FollowSets[nt])
                                {
                                    if (FollowSets[symbols[i]].Add(f))
                                        changed = true;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    
    private Dictionary<string, HashSet<string>> ComputeFirstSets()
    {
        var firstSets = new Dictionary<string, HashSet<string>>();
        foreach (var nt in NonTerminals)
            firstSets[nt] = new HashSet<string>();

        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var nt in NonTerminals)
            {
                foreach (var prod in Productions[nt])
                {
                    var symbols = prod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    
                    if (symbols.Length == 0 || symbols[0] == "ε" || symbols[0] == "epsilon")
                    {
                        if (firstSets[nt].Add("ε"))
                            changed = true;
                    }
                    else
                    {
                        bool allNullable = true;
                        foreach (var symbol in symbols)
                        {
                            if (Terminals.Contains(symbol))
                            {
                                if (firstSets[nt].Add(symbol))
                                    changed = true;
                                allNullable = false;
                                break;
                            }
                            else if (NonTerminals.Contains(symbol))
                            {
                                foreach (var f in firstSets[symbol].Where(x => x != "ε"))
                                {
                                    if (firstSets[nt].Add(f))
                                        changed = true;
                                }
                                if (!firstSets[symbol].Contains("ε"))
                                {
                                    allNullable = false;
                                    break;
                                }
                            }
                        }
                        if (allNullable && firstSets[nt].Add("ε"))
                            changed = true;
                    }
                }
            }
        }
        return firstSets;
    }
    
    private void BuildCanonicalCollection()
    {
        var initial = new LR0ItemSet();
        initial.Items.Add(new LR0Item { NonTerminal = AugmentedStart, Production = StartSymbol, DotPosition = 0 });
        initial = Closure(initial);
        
        CanonicalCollection.Add(initial);
        var unmarked = new Queue<int>();
        unmarked.Enqueue(0);
        
        while (unmarked.Count > 0)
        {
            var stateIdx = unmarked.Dequeue();
            var state = CanonicalCollection[stateIdx];
            
            var symbols = new HashSet<string>();
            foreach (var item in state.Items)
            {
                var nextSymbol = GetNextSymbol(item);
                if (nextSymbol != null)
                    symbols.Add(nextSymbol);
            }
            
            foreach (var symbol in symbols)
            {
                var gotoSet = Goto(state, symbol);
                if (gotoSet.Items.Count > 0)
                {
                    var existingIdx = FindExistingState(gotoSet);
                    if (existingIdx == -1)
                    {
                        CanonicalCollection.Add(gotoSet);
                        state.Transitions[symbol] = CanonicalCollection.Count - 1;
                        unmarked.Enqueue(CanonicalCollection.Count - 1);
                    }
                    else
                    {
                        state.Transitions[symbol] = existingIdx;
                    }
                }
            }
        }
    }
    
    private LR0ItemSet Closure(LR0ItemSet itemSet)
    {
        var result = new LR0ItemSet();
        foreach (var item in itemSet.Items)
            result.Items.Add(new LR0Item { NonTerminal = item.NonTerminal, Production = item.Production, DotPosition = item.DotPosition });
        
        bool changed = true;
        while (changed)
        {
            changed = false;
            var currentItems = result.Items.ToList();
            foreach (var item in currentItems)
            {
                var nextSymbol = GetNextSymbol(item);
                if (nextSymbol != null && NonTerminals.Contains(nextSymbol))
                {
                    foreach (var prod in Productions[nextSymbol])
                    {
                        var newItem = new LR0Item { NonTerminal = nextSymbol, Production = prod, DotPosition = 0 };
                        if (!result.Items.Any(i => i.NonTerminal == newItem.NonTerminal && i.Production == newItem.Production && i.DotPosition == newItem.DotPosition))
                        {
                            result.Items.Add(newItem);
                            changed = true;
                        }
                    }
                }
            }
        }
        
        return result;
    }
    
    private LR0ItemSet Goto(LR0ItemSet itemSet, string symbol)
    {
        var result = new LR0ItemSet();
        
        foreach (var item in itemSet.Items)
        {
            var nextSymbol = GetNextSymbol(item);
            if (nextSymbol == symbol)
            {
                result.Items.Add(new LR0Item 
                { 
                    NonTerminal = item.NonTerminal, 
                    Production = item.Production, 
                    DotPosition = item.DotPosition + 1 
                });
            }
        }
        
        if (result.Items.Count > 0)
            result = Closure(result);
        
        return result;
    }
    
    private string? GetNextSymbol(LR0Item item)
    {
        var symbols = item.Production.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (item.DotPosition < symbols.Length)
            return symbols[item.DotPosition];
        return null;
    }
    
    private int FindExistingState(LR0ItemSet itemSet)
    {
        for (int i = 0; i < CanonicalCollection.Count; i++)
        {
            if (ItemSetsEqual(CanonicalCollection[i], itemSet))
                return i;
        }
        return -1;
    }
    
    private bool ItemSetsEqual(LR0ItemSet set1, LR0ItemSet set2)
    {
        if (set1.Items.Count != set2.Items.Count)
            return false;
        
        foreach (var item1 in set1.Items)
        {
            if (!set2.Items.Any(item2 => item2.NonTerminal == item1.NonTerminal && 
                                         item2.Production == item1.Production && 
                                         item2.DotPosition == item1.DotPosition))
                return false;
        }
        return true;
    }
    
    private void BuildParsingTable()
    {
        for (int i = 0; i < CanonicalCollection.Count; i++)
        {
            var state = CanonicalCollection[i];
            
            foreach (var item in state.Items)
            {
                var nextSymbol = GetNextSymbol(item);
                
                if (nextSymbol != null && Terminals.Contains(nextSymbol))
                {
                    if (state.Transitions.ContainsKey(nextSymbol))
                    {
                        var nextState = state.Transitions[nextSymbol];
                        ActionTable[(i, nextSymbol)] = $"s{nextState}";
                    }
                }
                else if (nextSymbol == null)
                {
                    if (item.NonTerminal == AugmentedStart)
                    {
                        ActionTable[(i, "$")] = "acc";
                    }
                    else
                    {
                        var prodIdx = GetProductionIndex(item.NonTerminal, item.Production);
                        foreach (var follow in FollowSets[item.NonTerminal])
                        {
                            ActionTable[(i, follow)] = $"r{prodIdx}";
                        }
                    }
                }
            }
            
            foreach (var trans in state.Transitions)
            {
                if (NonTerminals.Contains(trans.Key))
                {
                    GotoTable[(i, trans.Key)] = trans.Value;
                }
            }
        }
    }
    
    private int GetProductionIndex(string nonTerminal, string production)
    {
        int idx = 0;
        foreach (var nt in Productions.Keys.OrderBy(k => k == AugmentedStart ? 0 : 1).ThenBy(k => k))
        {
            foreach (var prod in Productions[nt])
            {
                if (nt == nonTerminal && prod == production)
                    return idx;
                idx++;
            }
        }
        return -1;
    }
    
    public string GenerateLR0ItemsHTML()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<div class='lr0-items'>");
        
        for (int i = 0; i < CanonicalCollection.Count; i++)
        {
            sb.AppendLine($"<div class='item-set mb-3'>");
            sb.AppendLine($"<h6>I{i}:</h6>");
            sb.AppendLine("<pre>");
            
            foreach (var item in CanonicalCollection[i].Items)
            {
                var symbols = item.Production.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var prodStr = "";
                
                if (symbols.Length == 0 || symbols[0] == "ε")
                {
                    prodStr = item.DotPosition == 0 ? "•" : "";
                }
                else
                {
                    for (int j = 0; j < symbols.Length; j++)
                    {
                        if (j == item.DotPosition)
                            prodStr += "•";
                        prodStr += symbols[j];
                        if (j < symbols.Length - 1)
                            prodStr += " ";
                    }
                    if (item.DotPosition == symbols.Length)
                        prodStr += "•";
                }
                
                sb.AppendLine($"{item.NonTerminal} → {prodStr}");
            }
            
            sb.AppendLine("</pre>");
            sb.AppendLine("</div>");
        }
        
        sb.AppendLine("</div>");
        return sb.ToString();
    }
    
    public string GenerateParsingTableHTML()
    {
        var allTerminals = Terminals.OrderBy(t => t).ToList();
        var allNonTerminals = NonTerminals.Where(nt => nt != AugmentedStart).OrderBy(nt => nt).ToList();
        
        var sb = new StringBuilder();
        sb.AppendLine("<table class='table table-bordered table-sm'>");
        sb.AppendLine("<thead><tr><th>State</th>");
        
        sb.AppendLine("<th colspan='" + (allTerminals.Count + 1) + "' class='text-center'>ACTION</th>");
        sb.AppendLine("<th colspan='" + allNonTerminals.Count + "' class='text-center'>GOTO</th>");
        sb.AppendLine("</tr><tr><th></th>");
        
        foreach (var t in allTerminals)
            sb.AppendLine($"<th>{t}</th>");
        sb.AppendLine("<th>$</th>");
        
        foreach (var nt in allNonTerminals)
            sb.AppendLine($"<th>{nt}</th>");
        sb.AppendLine("</tr></thead><tbody>");
        
        for (int i = 0; i < CanonicalCollection.Count; i++)
        {
            sb.AppendLine($"<tr><td><strong>{i}</strong></td>");
            
            foreach (var t in allTerminals)
            {
                var action = ActionTable.GetValueOrDefault((i, t), "");
                sb.AppendLine($"<td>{action}</td>");
            }
            
            var dollarAction = ActionTable.GetValueOrDefault((i, "$"), "");
            sb.AppendLine($"<td>{dollarAction}</td>");
            
            foreach (var nt in allNonTerminals)
            {
                var gotoState = GotoTable.ContainsKey((i, nt)) ? GotoTable[(i, nt)].ToString() : "";
                sb.AppendLine($"<td>{gotoState}</td>");
            }
            
            sb.AppendLine("</tr>");
        }
        
        sb.AppendLine("</tbody></table>");
        return sb.ToString();
    }
    
    public List<string> Parse(string input)
    {
        var steps = new List<string>();
        var stack = new Stack<int>();
        stack.Push(0);
        
        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        tokens.Add("$");
        int index = 0;
        
        steps.Add("<table class='table table-bordered'><thead><tr><th>Stack</th><th>Input</th><th>Action</th></tr></thead><tbody>");
        
        while (true)
        {
            var state = stack.Peek();
            var currentToken = tokens[index];
            
            var stackStr = string.Join(" ", stack.Reverse());
            var inputStr = string.Join(" ", tokens.Skip(index));
            
            if (!ActionTable.ContainsKey((state, currentToken)))
            {
                steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td><span class='badge bg-danger'>Error: No action</span></td></tr>");
                break;
            }
            
            var action = ActionTable[(state, currentToken)];
            
            if (action == "acc")
            {
                steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td><span class='badge bg-success'>Accept</span></td></tr>");
                break;
            }
            else if (action.StartsWith("s"))
            {
                var nextState = int.Parse(action.Substring(1));
                steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td>Shift {nextState}</td></tr>");
                stack.Push(nextState);
                index++;
            }
            else if (action.StartsWith("r"))
            {
                var prodIdx = int.Parse(action.Substring(1));
                var (nt, prod) = GetProductionByIndex(prodIdx);
                
                var symbols = prod.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var popCount = (symbols.Length == 0 || symbols[0] == "ε") ? 0 : symbols.Length;
                
                for (int i = 0; i < popCount; i++)
                    stack.Pop();
                
                var topState = stack.Peek();
                if (GotoTable.ContainsKey((topState, nt)))
                {
                    var gotoState = GotoTable[(topState, nt)];
                    steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td>Reduce {nt} → {prod}</td></tr>");
                    stack.Push(gotoState);
                }
                else
                {
                    steps.Add($"<tr><td>{stackStr}</td><td>{inputStr}</td><td><span class='badge bg-danger'>Error: No GOTO</span></td></tr>");
                    break;
                }
            }
        }
        
        steps.Add("</tbody></table>");
        return steps;
    }
    
    private (string, string) GetProductionByIndex(int idx)
    {
        int current = 0;
        foreach (var nt in Productions.Keys.OrderBy(k => k == AugmentedStart ? 0 : 1).ThenBy(k => k))
        {
            foreach (var prod in Productions[nt])
            {
                if (current == idx)
                    return (nt, prod);
                current++;
            }
        }
        return ("", "");
    }
    
    public string GetGrammarWithNumbers()
    {
        var sb = new StringBuilder();
        int idx = 0;
        
        foreach (var nt in Productions.Keys.OrderBy(k => k == AugmentedStart ? 0 : 1).ThenBy(k => k))
        {
            foreach (var prod in Productions[nt])
            {
                sb.AppendLine($"({idx}) {nt} → {prod}");
                idx++;
            }
        }
        
        return sb.ToString();
    }
}

public class LR0Item
{
    public string NonTerminal { get; set; } = "";
    public string Production { get; set; } = "";
    public int DotPosition { get; set; }
}

public class LR0ItemSet
{
    public List<LR0Item> Items { get; set; } = new();
    public Dictionary<string, int> Transitions { get; set; } = new();
}

namespace Compiler_Designing_Virtual_Lab.Models;

public class RegexToNfaModel
{
    public string? Regex { get; set; }
    public List<string>? Steps { get; set; }
    public string? TransitionTable { get; set; }
    public string? DiagramSvg { get; set; }
}

public class NfaToDfaModel
{
    public string? NfaInput { get; set; }
    public List<string>? EpsilonClosures { get; set; }
    public List<string>? Steps { get; set; }
    public string? DfaTable { get; set; }
    public string? DiagramSvg { get; set; }
    public string? AsciiDiagram { get; set; }
}

public class DirectDfaModel
{
    public string? Regex { get; set; }
    public string? SyntaxTree { get; set; }
    public Dictionary<string, string>? ComputedValues { get; set; }
    public string? FollowposTable { get; set; }
    public string? DfaTable { get; set; }
    public string? DiagramSvg { get; set; }
}

public class DfaMinimizationModel
{
    public string? DfaInput { get; set; }
    public List<string>? Partitions { get; set; }
    public string? MinimizedDfa { get; set; }
    public string? BeforeDiagram { get; set; }
    public string? AfterDiagram { get; set; }
    public string? AsciiDiagram { get; set; }
}

public class LexicalAnalyzerModel
{
    public string? SourceCode { get; set; }
    public List<Token>? Tokens { get; set; }
    public List<Symbol>? SymbolTable { get; set; }
}

public class Token
{
    public string? Type { get; set; }
    public string? Value { get; set; }
    public int Line { get; set; }
}

public class Symbol
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Scope { get; set; }
}

public class ParserModel
{
    public string? Grammar { get; set; }
    public string? Input { get; set; }
    public Dictionary<string, HashSet<string>>? FirstSet { get; set; }
    public Dictionary<string, HashSet<string>>? FollowSet { get; set; }
    public string? ParsingTable { get; set; }
    public List<string>? ParseSteps { get; set; }
}

public class SLRParserModel
{
    public string? Grammar { get; set; }
    public string? Input { get; set; }
    public string? AugmentedGrammar { get; set; }
    public string? LR0Items { get; set; }
    public string? ParsingTable { get; set; }
    public List<string>? ParseSteps { get; set; }
}

public class ThreeAddressCodeModel
{
    public string? Expression { get; set; }
    public List<string>? TAC { get; set; }
    public List<Quadruple>? Quadruples { get; set; }
    public List<Triple>? Triples { get; set; }
}

public class Quadruple
{
    public string? Op { get; set; }
    public string? Arg1 { get; set; }
    public string? Arg2 { get; set; }
    public string? Result { get; set; }
}

public class Triple
{
    public string? Op { get; set; }
    public string? Arg1 { get; set; }
    public string? Arg2 { get; set; }
}

public class OptimizationModel
{
    public string? Code { get; set; }
    public string? BeforeCode { get; set; }
    public string? AfterCode { get; set; }
    public List<string>? OptimizationSteps { get; set; }
}

public class CodeGenerationModel
{
    public string? Code { get; set; }
    public List<string>? BasicBlocks { get; set; }
    public string? DagDiagram { get; set; }
    public string? GeneratedCode { get; set; }
}

public class AllPhasesModel
{
    public string? Statement { get; set; }
    public List<Token>? Tokens { get; set; }
    public string? ParseTree { get; set; }
    public List<string>? SemanticAnalysis { get; set; }
    public List<string>? IntermediateCode { get; set; }
    public List<string>? OptimizedCode { get; set; }
    public List<string>? TargetCode { get; set; }
    public List<Symbol>? SymbolTable { get; set; }
}

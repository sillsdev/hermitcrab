﻿using System.Collections.Generic;
using System.Text;
using SIL.Machine;
using SIL.Machine.FeatureModel;

namespace SIL.HermitCrab
{
    /// <summary>
    /// This class represents a trace record. All trace records inherit from this class.
    /// A morph trace is a tree structure where each node in the tree is a <c>Trace</c> object.
    /// </summary>
    public abstract class Trace
    {
        /// <summary>
        /// The type of trace record
        /// </summary>
        public enum TraceType
        {
            /// <summary>
            /// Word analysis trace
            /// </summary>
            WordAnalysis,
            /// <summary>
            /// Stratum synthesis trace
            /// </summary>
            StratumSynthesis,
            /// <summary>
            /// Stratum analysis trace
            /// </summary>
            StratumAnalysis,
            /// <summary>
            /// Lexical lookup trace
            /// </summary>
            LexicalLookup,
            /// <summary>
            /// Blocking trace
            /// </summary>
            Blocking,
            /// <summary>
            /// Word synthesis trace
            /// </summary>
            WordSynthesis,
            /// <summary>
            /// Phonological rule analysis trace
            /// </summary>
            PhonologicalRuleAnalysis,
            /// <summary>
            /// Phonological rule synthesis trace
            /// </summary>
            PhonologicalRuleSynthesis,
            /// <summary>
            /// Affix template analysis trace
            /// </summary>
            TemplateAnalysis,
            /// <summary>
            /// Affix template synthesis trace
            /// </summary>
            TemplateSynthesis,
            /// <summary>
            /// Morphological rule analysis trace
            /// </summary>
            MorphologicalRuleAnalysis,
            /// <summary>
            /// Morphological rule synthesis trace
            /// </summary>
            MorphologicalRuleSynthesis,
            /// <summary>
            /// Report success trace
            /// </summary>
            ReportSuccess
        }

    	private readonly List<Trace> _children;

        /// <summary>
        /// Initializes a new instance of the <see cref="Trace"/> class.
        /// </summary>
        internal Trace()
        {
            _children = new List<Trace>();
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public abstract TraceType Type
        {
            get;
        }

        /// <summary>
        /// Gets the children of this trace record.
        /// </summary>
        /// <value>The children.</value>
        public IEnumerable<Trace> Children
        {
            get
            {
                return _children;
            }
        }

        /// <summary>
        /// Gets the child count.
        /// </summary>
        /// <value>The child count.</value>
        public int ChildCount
        {
            get
            {
                return _children.Count;
            }
        }

        /// <summary>
        /// Gets the child at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The child.</returns>
        public Trace GetChildAt(int index)
        {
            return _children[index];
        }

        /// <summary>
        /// Adds the child.
        /// </summary>
        /// <param name="tr">The trace record.</param>
        internal void AddChild(Trace tr)
        {
            _children.Add(tr);
        }

        public override string ToString()
        {
            return ToString(true);
        }

        public abstract string ToString(bool includeInputs);
    }

    /// <summary>
    /// This represents the root of a word analysis trace tree.
    /// </summary>
    public class WordAnalysisTrace : Trace
    {
        private readonly string _inputWord;
        private readonly Shape _inputShape;

        /// <summary>
		/// Initializes a new instance of the <see cref="WordAnalysisTrace"/> class.
        /// </summary>
        /// <param name="inputWord">The input word.</param>
        /// <param name="inputShape">The input shape.</param>
        internal WordAnalysisTrace(string inputWord, Shape inputShape)
        {
            _inputWord = inputWord;
            _inputShape = inputShape;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.WordAnalysis;
            }
        }

        /// <summary>
        /// Gets the input word.
        /// </summary>
        /// <value>The input word.</value>
        public string InputWord
        {
            get
            {
                return _inputWord;
            }
        }

        /// <summary>
        /// Gets the input shape.
        /// </summary>
        /// <value>The input shape.</value>
        public Shape InputShape
        {
            get
            {
                return _inputShape;
            }
        }

        public override string ToString(bool includeInputs)
        {
            return string.Format(HCStrings.kstidTraceWordAnalysis, _inputWord);
        }
    }

    /// <summary>
    /// This abstract class is used to represent all stratum-related trace records.
    /// </summary>
    public abstract class StratumTrace : Trace
    {
        private readonly Stratum _stratum;
        private readonly bool _input;

        /// <summary>
        /// Initializes a new instance of the <see cref="StratumTrace"/> class.
        /// </summary>
        /// <param name="stratum">The stratum.</param>
        /// <param name="input">if <c>true</c> this is an input record, if <c>false</c> this is an output record.</param>
        internal StratumTrace(Stratum stratum, bool input)
        {
            _stratum = stratum;
            _input = input;
        }

        /// <summary>
        /// Gets the stratum.
        /// </summary>
        /// <value>The stratum.</value>
        public Stratum Stratum
        {
            get
            {
                return _stratum;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is an input or output trace record.
        /// </summary>
        /// <value><c>true</c> if this instance is an input record; otherwise it is an output record.</value>
        public bool IsInput
        {
            get
            {
                return _input;
            }
        }
    }

    /// <summary>
    /// This is used to represent information resulting from the tracing of strata
    /// during analysis. This trace record is produced before and after a stratum is
    /// unapplied to a word analysis.
    /// </summary>
    public class StratumAnalysisTrace : StratumTrace
    {
        private readonly Word _word;

        /// <summary>
        /// Initializes a new instance of the <see cref="StratumAnalysisTrace"/> class.
        /// </summary>
        /// <param name="stratum">The stratum.</param>
        /// <param name="input">if <c>true</c> this is an input record, if <c>false</c> this is an output record.</param>
        /// <param name="word">The input or output word analysis.</param>
        internal StratumAnalysisTrace(Stratum stratum, bool input, Word word)
            : base(stratum, input)
        {
            _word = word;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.StratumAnalysis;
            }
        }

        /// <summary>
        /// Gets the input or output word analysis.
        /// </summary>
        /// <value>The input or output word analysis.</value>
        public Word Word
        {
            get
            {
                return _word;
            }
        }

        public override string ToString(bool includeInputs)
        {
        	if (IsInput)
            {
                return string.Format(HCStrings.kstidTraceStratumAnalysisIn, Stratum,
                    _word.Stratum.SymbolDefinitionTable.ToRegexString(_word.Shape, true));
            }

        	return string.Format(HCStrings.kstidTraceStratumAnalysisOut, Stratum,
				_word.Stratum.SymbolDefinitionTable.ToRegexString(_word.Shape, true));
        }
    }

    /// <summary>
    /// This is used to represent information resulting from the tracing of strata
    /// during synthesis. This trace record is produced every time a stratum is applied
    /// to a word synthesis.
    /// </summary>
    public class StratumSynthesisTrace : StratumTrace
    {
        private readonly Word _word;

        /// <summary>
        /// Initializes a new instance of the <see cref="StratumSynthesisTrace"/> class.
        /// </summary>
        /// <param name="stratum">The stratum.</param>
        /// <param name="input">if <c>true</c> this is an input record, if <c>false</c> this is an output record.</param>
        /// <param name="word">The input or output word synthesis.</param>
        internal StratumSynthesisTrace(Stratum stratum, bool input, Word word)
            : base(stratum, input)
        {
            _word = word;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.StratumSynthesis;
            }
        }

        /// <summary>
        /// Gets the input or output word synthesis.
        /// </summary>
        /// <value>The input or output word synthesis.</value>
        public Word Word
        {
            get
            {
                return _word;
            }
        }

        public override string ToString(bool includeInputs)
        {
        	if (IsInput)
            {
                return string.Format(HCStrings.kstidTraceStratumSynthesisIn, Stratum,
                    _word.Stratum.SymbolDefinitionTable.ToString(_word.Shape, true));
            }

        	return string.Format(HCStrings.kstidTraceStratumSynthesisOut, Stratum,
				_word.Stratum.SymbolDefinitionTable.ToString(_word.Shape, true));
        }
    }

    /// <summary>
    /// This is used to represent information resulting from the tracing of lexical lookup in a
    /// stratum during analysis. This trace record is produced every time a lexical lookup is
    /// attempted. If the lookup successfully finds entries that match the input shape a word synthesis
    /// trace record will be created and added as a child.
    /// </summary>
    public class LexLookupTrace : Trace
    {
        private readonly Shape _shape;
        private readonly Stratum _stratum;

        /// <summary>
        /// Initializes a new instance of the <see cref="LexLookupTrace"/> class.
        /// </summary>
        /// <param name="stratum">The stratum.</param>
        /// <param name="shape">The shape.</param>
        internal LexLookupTrace(Stratum stratum, Shape shape)
        {
            _stratum = stratum;
            _shape = shape;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.LexicalLookup;
            }
        }

        /// <summary>
        /// Gets the stratum.
        /// </summary>
        /// <value>The stratum.</value>
        public Stratum Stratum
        {
            get
            {
                return _stratum;
            }
        }

        /// <summary>
        /// Gets the input shape.
        /// </summary>
        /// <value>The shape.</value>
        public Shape Shape
        {
            get
            {
                return _shape;
            }
        }

        public override string ToString(bool includeInputs)
        {
            return string.Format(HCStrings.kstidTraceLexLookup,
                _stratum.SymbolDefinitionTable.ToRegexString(_shape, true), _stratum);
        }
    }

    /// <summary>
    /// This represents the root of a word synthesis trace tree. This trace record is usually produced
    /// when a lexical lookup successfully returns a matching lexical entry. 
    /// </summary>
    public class WordSynthesisTrace : Trace
    {
        private readonly RootAllomorph _rootAllomorph;
        private readonly FeatureStruct _rzFeatures;
        private readonly List<MorphologicalRule> _mrules;

        /// <summary>
        /// Initializes a new instance of the <see cref="WordSynthesisTrace"/> class.
        /// </summary>
        /// <param name="rootAllomorph">The root allomorph.</param>
        /// <param name="mrules">The morphological rules.</param>
        /// <param name="rzFeatures">The realizational features.</param>
        internal WordSynthesisTrace(RootAllomorph rootAllomorph, IEnumerable<MorphologicalRule> mrules, FeatureStruct rzFeatures)
        {
            _rootAllomorph = rootAllomorph;
            _mrules = new List<MorphologicalRule>(mrules);
            _rzFeatures = rzFeatures;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.WordSynthesis;
            }
        }

        /// <summary>
        /// Gets the root allomorph.
        /// </summary>
        /// <value>The root allomorph.</value>
        public RootAllomorph RootAllomorph
        {
            get
            {
                return _rootAllomorph;
            }
        }

        /// <summary>
        /// Gets the morphological rules.
        /// </summary>
        /// <value>The morphological rules.</value>
        public IEnumerable<MorphologicalRule> MorphologicalRules
        {
            get
            {
                return _mrules;
            }
        }

        /// <summary>
        /// Gets the realizational features.
        /// </summary>
        /// <value>The realizational features.</value>
        public FeatureStruct RealizationalFeatures
        {
            get
            {
                return _rzFeatures;
            }
        }

        public override string ToString(bool includeInputs)
        {
            var sb = new StringBuilder();
            bool firstItem = true;
            foreach (MorphologicalRule rule in _mrules)
            {
                if (!firstItem)
                    sb.Append(", ");
                sb.Append(rule.Description);
                firstItem = false;
            }

            return string.Format(HCStrings.kstidTraceWordSynthesis, _rootAllomorph.Morpheme,
                _rootAllomorph.Morpheme.Stratum.SymbolDefinitionTable.ToString(_rootAllomorph.Shape, true),
                sb, _rzFeatures);
        }
    }

    /// <summary>
    /// This abstract class is used to represent all phonological rule-related trace records.
    /// </summary>
    public abstract class PhonologicalRuleTrace : Trace
    {
        private readonly StandardPhonologicalRule _rule;

        /// <summary>
        /// Initializes a new instance of the <see cref="PhonologicalRuleTrace"/> class.
        /// </summary>
        /// <param name="rule">The rule.</param>
        internal PhonologicalRuleTrace(StandardPhonologicalRule rule)
        {
            _rule = rule;
        }

        /// <summary>
        /// Gets the rule.
        /// </summary>
        /// <value>The rule.</value>
        public StandardPhonologicalRule Rule
        {
            get
            {
                return _rule;
            }
        }

    }

    /// <summary>
    /// This is used to represent information resulting from the tracing of phonological rules
    /// during analysis. This trace record is produced every time a phonological rule is unapplied
    /// to a word analysis.
    /// </summary>
    public class PhonologicalRuleAnalysisTrace : PhonologicalRuleTrace
    {
        private readonly Word _input;

    	/// <summary>
        /// Initializes a new instance of the <see cref="PhonologicalRuleAnalysisTrace"/> class.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="input">The input.</param>
        internal PhonologicalRuleAnalysisTrace(StandardPhonologicalRule rule, Word input)
            : base(rule)
        {
            _input = input;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.PhonologicalRuleAnalysis;
            }
        }

        /// <summary>
        /// Gets the input word analysis.
        /// </summary>
        /// <value>The input word analysis.</value>
        public Word Input
        {
            get
            {
                return _input;
            }
        }

    	/// <summary>
    	/// Gets or sets the output word analysis.
    	/// </summary>
    	/// <value>The output word analysis.</value>
    	public Word Output { get; internal set; }

    	public override string ToString(bool includeInputs)
    	{
    		if (includeInputs)
            {
                return string.Format(HCStrings.kstidTracePhonologicalRuleAnalysisInputs, Rule,
                    _input.Stratum.SymbolDefinitionTable.ToRegexString(_input.Shape, true),
					Output == null ? HCStrings.kstidTraceNoOutput : Output.Stratum.SymbolDefinitionTable.ToRegexString(Output.Shape, true));
            }

    		return string.Format(HCStrings.kstidTracePhonologicalRuleAnalysis, Rule,
				Output == null ? HCStrings.kstidTraceNoOutput : Output.Stratum.SymbolDefinitionTable.ToRegexString(Output.Shape, true));
    	}
    }

    /// <summary>
    /// This is used to represent information resulting from the tracing of phonological rules
    /// during synthesis. This trace record is produced every time a phonological rule is applied
    /// to a word synthesis.
    /// </summary>
    public class PhonologicalRuleSynthesisTrace : PhonologicalRuleTrace
    {
    	private readonly Word _input;

    	/// <summary>
        /// Initializes a new instance of the <see cref="PhonologicalRuleSynthesisTrace"/> class.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="input">The input.</param>
        internal PhonologicalRuleSynthesisTrace(StandardPhonologicalRule rule, Word input)
            : base(rule)
        {
            _input = input;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.PhonologicalRuleSynthesis;
            }
        }

        /// <summary>
        /// Gets the input word synthesis.
        /// </summary>
        /// <value>The input word synthesis.</value>
        public Word Input
        {
            get
            {
                return _input;
            }
        }

    	/// <summary>
    	/// Gets or sets the output word synthesis.
    	/// </summary>
    	/// <value>The output word synthesis.</value>
    	public Word Output { get; internal set; }

    	public override string ToString(bool includeInputs)
    	{
    		if (includeInputs)
            {
                return string.Format(HCStrings.kstidTracePhonologicalRuleSynthesisInputs, Rule,
                    _input.Stratum.SymbolDefinitionTable.ToString(_input.Shape, true),
					Output == null ? HCStrings.kstidTraceNoOutput : Output.Stratum.SymbolDefinitionTable.ToString(Output.Shape, true));
            }

    		return string.Format(HCStrings.kstidTracePhonologicalRuleSynthesis, Rule,
				Output == null ? HCStrings.kstidTraceNoOutput : Output.Stratum.SymbolDefinitionTable.ToString(Output.Shape, true));
    	}
    }

    /// <summary>
    /// This abstract class is used to represent all affix template-related trace records.
    /// </summary>
    public abstract class TemplateTrace : Trace
    {
        private readonly AffixTemplate _template;
        private readonly bool _input;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateTrace"/> class.
        /// </summary>
        /// <param name="template">The template.</param>
        /// <param name="input">if <c>true</c> this is an input record, if <c>false</c> this is an output record.</param>
        internal TemplateTrace(AffixTemplate template, bool input)
        {
            _template = template;
            _input = input;
        }

        /// <summary>
        /// Gets the affix template.
        /// </summary>
        /// <value>The affix template.</value>
        public AffixTemplate Template
        {
            get
            {
                return _template;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is an input or output trace record.
        /// </summary>
        /// <value><c>true</c> if this instance is an input record, otherwise it is an output record.</value>
        public bool IsInput
        {
            get
            {
                return _input;
            }
        }
    }

    /// <summary>
    /// This is used to represent information resulting from the tracing of affix templates
    /// during analysis. This trace record is produced before and after an attempt to unapply an
    /// affix template to a word analysis is made.
    /// </summary>
    public class TemplateAnalysisTrace : TemplateTrace
    {
        private readonly Word _word;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateAnalysisTrace"/> class.
        /// </summary>
        /// <param name="template">The template.</param>
        /// <param name="input">if <c>true</c> this is an input record, if <c>false</c> this is an output record.</param>
        /// <param name="word">The input or output word analysis.</param>
        internal TemplateAnalysisTrace(AffixTemplate template, bool input, Word word)
            : base(template, input)
        {
            _word = word;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.TemplateAnalysis;
            }
        }

        /// <summary>
        /// Gets the input or output word analysis.
        /// </summary>
        /// <value>The input or output word analysis.</value>
        public Word Word
        {
            get
            {
                return _word;
            }
        }

        public override string ToString(bool includeInputs)
        {
        	if (IsInput)
            {
                return string.Format(HCStrings.kstidTraceTemplateAnalysisIn, Template,
                    _word.Stratum.SymbolDefinitionTable.ToRegexString(_word.Shape, true));
            }

        	return string.Format(HCStrings.kstidTraceTemplateAnalysisOut, Template,
				(_word == null ? HCStrings.kstidTraceNoOutput : _word.Stratum.SymbolDefinitionTable.ToRegexString(_word.Shape, true)));
        }
    }

    /// <summary>
    /// This is used to represent information resulting from the tracing of affix templates
    /// during synthesis. This trace record is produced before and after an attempt to apply an
    /// affix template to a word synthesis is made.
    /// </summary>
    public class TemplateSynthesisTrace : TemplateTrace
    {
        private readonly Word _word;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateSynthesisTrace"/> class.
        /// </summary>
        /// <param name="template">The template.</param>
        /// <param name="input">if <c>true</c> this is an input record, if <c>false</c> this is an output record.</param>
        /// <param name="word">The input or output word synthesis.</param>
        internal TemplateSynthesisTrace(AffixTemplate template, bool input, Word word)
            : base(template, input)
        {
            _word = word;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.TemplateSynthesis;
            }
        }

        /// <summary>
        /// Gets the input or output word.
        /// </summary>
        /// <value>The input or output word.</value>
        public Word Word
        {
            get
            {
                return _word;
            }
        }

        public override string ToString(bool includeInputs)
        {
        	if (IsInput)
            {
                return string.Format(HCStrings.kstidTraceTemplateSynthesisIn, Template,
                    _word.Stratum.SymbolDefinitionTable.ToString(_word.Shape, true));
            }

        	return string.Format(HCStrings.kstidTraceTemplateSynthesisOut, Template,
				(_word == null ? HCStrings.kstidTraceNoOutput : _word.Stratum.SymbolDefinitionTable.ToString(_word.Shape, true)));
        }
    }

    /// <summary>
    /// This abstract class is used to represent all morphological rule-related trace records.
    /// </summary>
    public abstract class MorphologicalRuleTrace : Trace
    {
        private readonly MorphologicalRule _rule;

    	/// <summary>
        /// Initializes a new instance of the <see cref="MorphologicalRuleTrace"/> class.
        /// </summary>
        /// <param name="rule">The rule.</param>
        internal MorphologicalRuleTrace(MorphologicalRule rule)
        {
        	RuleAllomorph = null;
        	_rule = rule;
        }

    	/// <summary>
        /// Gets the rule.
        /// </summary>
        /// <value>The rule.</value>
        public MorphologicalRule Rule
        {
            get
            {
                return _rule;
            }
        }

    	/// <summary>
    	/// Gets or sets the rule allomorph.
    	/// </summary>
    	/// <value>The rule allomorph.</value>
    	public Allomorph RuleAllomorph { get; set; }
    }

    /// <summary>
    /// This is used to represent information resulting from the tracing of morphological rules
    /// during analysis. This trace record is produced every time an attempt to unapply a
    /// morphological rule to a word analysis is made. If the morphological rule was successfully
    /// unapplied the record will contain the output.
    /// </summary>
    public class MorphologicalRuleAnalysisTrace : MorphologicalRuleTrace
    {
        private readonly Word _input;

    	/// <summary>
        /// Initializes a new instance of the <see cref="MorphologicalRuleAnalysisTrace"/> class.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="input">The input.</param>
        internal MorphologicalRuleAnalysisTrace(MorphologicalRule rule, Word input)
            : base(rule)
        {
        	Output = null;
        	_input = input;
        }

    	/// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.MorphologicalRuleAnalysis;
            }
        }

        /// <summary>
        /// Gets the input word analysis.
        /// </summary>
        /// <value>The input word analysis.</value>
        public Word Input
        {
            get
            {
                return _input;
            }
        }

    	/// <summary>
    	/// Gets or sets the output word analysis.
    	/// </summary>
    	/// <value>The output word analysis.</value>
    	public Word Output { get; internal set; }

    	public override string ToString(bool includeInputs)
    	{
    		if (includeInputs)
            {
                return string.Format(HCStrings.kstidTraceMorphologicalRuleAnalysisInputs, Rule,
                    _input.Stratum.SymbolDefinitionTable.ToRegexString(_input.Shape, true),
                    (Output == null ? HCStrings.kstidTraceNoOutput : Output.Stratum.SymbolDefinitionTable.ToRegexString(Output.Shape, true)));
            }

    		return string.Format(HCStrings.kstidTraceMorphologicalRuleAnalysis, Rule,
				(Output == null ? HCStrings.kstidTraceNoOutput : Output.Stratum.SymbolDefinitionTable.ToRegexString(Output.Shape, true)));
    	}
    }

    /// <summary>
    /// This is used to represent information resulting from the tracing of morphological rules
    /// during synthesis. This trace record is produced every time an attempt to apply a
    /// morphological rule to a word synthesis is made. If the morphological rule was successfully
    /// applied the record will contain the output.
    /// </summary>
    public class MorphologicalRuleSynthesisTrace : MorphologicalRuleTrace
    {
        private readonly Word _input;

    	/// <summary>
        /// Initializes a new instance of the <see cref="MorphologicalRuleSynthesisTrace"/> class.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="input">The input.</param>
        internal MorphologicalRuleSynthesisTrace(MorphologicalRule rule, Word input)
            : base(rule)
        {
        	Output = null;
        	_input = input;
        }

    	/// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.MorphologicalRuleSynthesis;
            }
        }

        /// <summary>
        /// Gets the input word synthesis.
        /// </summary>
        /// <value>The input word synthesis.</value>
        public Word Input
        {
            get
            {
                return _input;
            }
        }

    	/// <summary>
    	/// Gets or sets the output word synthesis.
    	/// </summary>
    	/// <value>The output word synthesis.</value>
    	public Word Output { get; internal set; }

    	public override string ToString(bool includeInputs)
    	{
    		if (includeInputs)
            {
                return string.Format(HCStrings.kstidTraceMorphologicalRuleSynthesisInputs, Rule,
                    _input.Stratum.SymbolDefinitionTable.ToString(_input.Shape, true),
                    (Output == null ? HCStrings.kstidTraceNoOutput
                    : Output.Stratum.SymbolDefinitionTable.ToString(Output.Shape, true)));
            }

    		return string.Format(HCStrings.kstidTraceMorphologicalRuleSynthesis, Rule,
				(Output == null ? HCStrings.kstidTraceNoOutput : Output.Stratum.SymbolDefinitionTable.ToString(Output.Shape, true)));
    	}
    }

    /// <summary>
    /// This is used to represent information resulting from the blocking of word synthesis by a lexical
    /// entry during synthesis. This trace record is produced every time a word synthesis is blocked.
    /// </summary>
    public class BlockingTrace : Trace
    {
        /// <summary>
        /// The block type
        /// </summary>
        public enum BlockType { Rule, Template }

        private readonly BlockType _blockingType;
        private readonly LexEntry _blockingEntry;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockingTrace"/> class.
        /// </summary>
        /// <param name="blockingType">Type of the blocking.</param>
        /// <param name="blockingEntry">The blocking entry.</param>
        internal BlockingTrace(BlockType blockingType, LexEntry blockingEntry)
        {
            _blockingType = blockingType;
            _blockingEntry = blockingEntry;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.Blocking;
            }
        }

        /// <summary>
        /// Gets the type of the blocking.
        /// </summary>
        /// <value>The type of the blocking.</value>
        public BlockType BlockingType
        {
            get
            {
                return _blockingType;
            }
        }

        /// <summary>
        /// Gets the blocking entry.
        /// </summary>
        /// <value>The blocking entry.</value>
        public LexEntry BlockingEntry
        {
            get
            {
                return _blockingEntry;
            }
        }

        public override string ToString(bool includeInputs)
        {
            string typeStr = null;
            switch (_blockingType)
            {
                case BlockType.Rule:
                    typeStr = "rule";
                    break;

                case BlockType.Template:
                    typeStr = "template";
                    break;
            }

            return string.Format(HCStrings.kstidTraceBlocking, typeStr, _blockingEntry);
        }
    }
    /// <summary>
    /// This is used to represent information resulting from the tracing of lexical lookup in a
    /// stratum during analysis. This trace record is produced every time a lexical lookup is
    /// attempted. If the lookup successfully finds entries that match the input shape a word synthesis
    /// trace record will be created and added as a child.
    /// </summary>
    public class ReportSuccessTrace : Trace
    {
        private readonly Word _output;
        /// <summary>
        /// Initializes a new instance of the <see cref="ReportSuccessTrace"/> class.
        /// </summary>
        internal ReportSuccessTrace(Word output)
        {
            _output = output;
        }

        /// <summary>
        /// Gets the trace record type.
        /// </summary>
        /// <value>The trace record type.</value>
        public override TraceType Type
        {
            get
            {
                return TraceType.ReportSuccess;
            }
        }

		/// <summary>
		/// Gets the output.
		/// </summary>
		/// <value>The output.</value>
		public Word Output
		{
			get
			{
				return _output;
			}
		}

        public override string ToString(bool includeInputs)
        {
            return string.Format(HCStrings.kstidTraceReportSuccess,
                _output.Stratum.SymbolDefinitionTable.ToString(_output.Shape, true));
        }
    }

}
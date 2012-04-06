﻿using System.Collections.Generic;
using System.Linq;
using SIL.Collections;
using SIL.Machine;
using SIL.Machine.FeatureModel;
using SIL.Machine.Rules;

namespace SIL.HermitCrab
{
	internal class AnalysisAffixTemplateRule : RuleCascade<Word, ShapeNode>
	{
		private readonly Morpher _morpher;
		private readonly AffixTemplate _template;

		public AnalysisAffixTemplateRule(SpanFactory<ShapeNode> spanFactory, Morpher morpher, AffixTemplate template)
			: base(CreateRules(spanFactory, morpher, template), RuleCascadeOrder.Permutation, FreezableEqualityComparer<Word>.Instance)
		{
			_morpher = morpher;
			_template = template;
		}

		private static IEnumerable<IRule<Word, ShapeNode>> CreateRules(SpanFactory<ShapeNode> spanFactory, Morpher morpher, AffixTemplate template)
		{
			foreach (AffixTemplateSlot slot in template.Slots.Reverse())
				yield return new RuleBatch<Word, ShapeNode>(slot.Rules.Select(mr => mr.CompileAnalysisRule(spanFactory, morpher)), true);
		}

		public override IEnumerable<Word> Apply(Word input)
		{
			FeatureStruct fs;
			if (!input.SyntacticFeatureStruct.Unify(_template.RequiredSyntacticFeatureStruct, out fs))
				return Enumerable.Empty<Word>();

			if (_morpher.TraceRules.Contains(_template))
				input.CurrentTrace.Children.Add(new Trace(TraceType.TemplateAnalysisInput, _template) {Input = input});
			Word[] results = base.Apply(input).ToArray();
			foreach (Word result in results)
			{
				if (_morpher.TraceRules.Contains(_template))
					result.CurrentTrace.Children.Add(new Trace(TraceType.TemplateAnalysisOutput, _template) {Output = result});
				result.SyntacticFeatureStruct = fs;
			}
			return results;
		}

		protected override bool Continue(IRule<Word, ShapeNode> rule, int index, Word input)
		{
			IList<AffixTemplateSlot> slots = _template.Slots;
			bool cont = slots[slots.Count - index - 1].Optional;
			if (!cont && _morpher.TraceRules.Contains(_template))
				input.CurrentTrace.Children.Add(new Trace(TraceType.TemplateAnalysisOutput, _template));
			return cont;
		}
	}
}
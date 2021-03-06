using System.Linq;
using SIL.Machine.Annotations;
using SIL.Machine.FeatureModel;
using SIL.Machine.Matching;

namespace SIL.HermitCrab.PhonologicalRules
{
	public class NarrowSynthesisRewriteSubruleSpec : SynthesisRewriteSubruleSpec
	{
		private readonly Pattern<Word, ShapeNode> _rhs; 
		private readonly int _targetCount;

		public NarrowSynthesisRewriteSubruleSpec(SpanFactory<ShapeNode> spanFactory, MatcherSettings<ShapeNode> matcherSettings, bool isIterative,
			int targetCount, RewriteSubrule subrule, int index)
			: base(spanFactory, matcherSettings, isIterative, subrule, index)
		{
			_rhs = subrule.Rhs;
			_targetCount = targetCount;
		}

		public override void ApplyRhs(Match<Word, ShapeNode> targetMatch, Span<ShapeNode> span, VariableBindings varBindings)
		{
			ShapeNode curNode = span.End;
			foreach (PatternNode<Word, ShapeNode> node in _rhs.Children)
			{
				var constraint = (Constraint<Word, ShapeNode>) node;
				FeatureStruct fs = constraint.FeatureStruct.DeepClone();
				if (varBindings != null)
					fs.ReplaceVariables(varBindings);
				curNode = targetMatch.Input.Shape.AddAfter(curNode, fs);
				if (IsIterative)
					curNode.SetDirty(true);
			}

			ShapeNode[] nodes = targetMatch.Input.Shape.GetNodes(span).ToArray();
			for (int i = 0; i < _targetCount; i++)
				nodes[i].SetDeleted(true);

			MarkSuccessfulApply(targetMatch.Input);
		}
	}
}

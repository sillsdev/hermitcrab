﻿using System;
using System.Collections.Generic;
using System.Linq;
using SIL.Machine;
using SIL.Machine.FeatureModel;
using SIL.Machine.Matching;
using SIL.Machine.Transduction;

namespace SIL.HermitCrab
{
	public class FeatureAnalysisRewriteRule : AnalysisRewriteRule
	{
		private readonly Expression<Word, ShapeNode> _rhs;
		private readonly Expression<Word, ShapeNode> _leftEnv;
		private readonly Expression<Word, ShapeNode> _rightEnv;
		private readonly Expression<Word, ShapeNode> _analysisRhs;
		private AnalysisReapplyType _reapplyType;

		public FeatureAnalysisRewriteRule(SpanFactory<ShapeNode> spanFactory, Expression<Word, ShapeNode> lhs, Expression<Word, ShapeNode> rhs,
			Expression<Word, ShapeNode> leftEnv, Expression<Word, ShapeNode> rightEnv)
			: base(spanFactory)
		{
			_rhs = rhs;
			_leftEnv = leftEnv;
			_rightEnv = rightEnv;

			ApplicationMode = ApplicationMode.Iterative;
			Direction = Direction.RightToLeft;
			var rhsAntiFSs = new List<FeatureStruct>();
			foreach (Constraint<Word, ShapeNode> constraint in rhs.Children.OfType<Constraint<Word, ShapeNode>>().Where(c => c.Type == HCFeatureSystem.SegmentType))
			{
				FeatureStruct fs = GetAntiFeatureStruct(constraint.FeatureStruct);
				fs.RemoveValue(AnnotationFeatureSystem.Type);
				rhsAntiFSs.Add(fs);
			}
			Lhs.Acceptable = (input, match) => IsUnapplicationNonvacuous(match, rhsAntiFSs);

			_analysisRhs = new Expression<Word, ShapeNode>();
			AddEnvironment("leftEnv", leftEnv);
			int i = 0;
			foreach (Tuple<PatternNode<Word, ShapeNode>, PatternNode<Word, ShapeNode>> tuple in lhs.Children.Zip(rhs.Children))
			{
				var lhsConstraint = (Constraint<Word, ShapeNode>) tuple.Item1;
				var rhsConstraint = (Constraint<Word, ShapeNode>) tuple.Item2;

				if (lhsConstraint.Type == HCFeatureSystem.SegmentType && rhsConstraint.Type == HCFeatureSystem.SegmentType)
				{
					var targetConstraint = (Constraint<Word, ShapeNode>)lhsConstraint.Clone();
					targetConstraint.FeatureStruct.PriorityUnion(rhsConstraint.FeatureStruct);
					targetConstraint.FeatureStruct.AddValue(HCFeatureSystem.Backtrack, HCFeatureSystem.NotSearched);
					Lhs.Children.Add(new Group<Word, ShapeNode>("target" + i) {Children = {targetConstraint}});

					var fs = GetAntiFeatureStruct(rhsConstraint.FeatureStruct);
					fs.Subtract(GetAntiFeatureStruct(lhsConstraint.FeatureStruct));
					_analysisRhs.Children.Add(new Constraint<Word, ShapeNode>(HCFeatureSystem.SegmentType, fs));

					i++;
				}
			}
			AddEnvironment("rightEnv", rightEnv);
		}

		public override Direction SynthesisDirection
		{
			set
			{
				base.SynthesisDirection = value;
				Direction = value == Direction.LeftToRight ? Direction.RightToLeft : Direction.LeftToRight;
			}
		}

		public override ApplicationMode SynthesisApplicationMode
		{
			set
			{
				base.SynthesisApplicationMode = value;
				if (value == ApplicationMode.Simultaneous)
				{
					_reapplyType = AnalysisReapplyType.Normal;
					foreach (Constraint<Word, ShapeNode> constraint in _rhs.Children)
					{
						if (constraint.Type == HCFeatureSystem.SegmentType)
						{
							if (!IsUnifiable(constraint, _leftEnv) || !IsUnifiable(constraint, _rightEnv))
							{
								_reapplyType = AnalysisReapplyType.SelfOpaquing;
								break;
							}
						}
					}
				}
			}
		}

		private bool IsUnapplicationNonvacuous(PatternMatch<ShapeNode> match, IEnumerable<FeatureStruct> rhsAntiFSs)
		{
			int i = 0;
			foreach (FeatureStruct fs in rhsAntiFSs)
			{
				ShapeNode node = match["target" + i].GetStart(Direction);
				if (!node.Annotation.FeatureStruct.IsUnifiable(fs, match.VariableBindings))
					return true;
				i++;
			}

			return false;
		}

		private static FeatureStruct GetAntiFeatureStruct(FeatureStruct fs)
		{
			var result = new FeatureStruct();
			foreach (Feature feature in fs.Features)
			{
				FeatureValue value = fs.GetValue(feature);
				var childFS = value as FeatureStruct;
				FeatureValue newValue;
				if (childFS != null)
					newValue = GetAntiFeatureStruct(childFS);
				else
					newValue = ((SimpleFeatureValue) value).Negation();
				result.AddValue(feature, newValue);
			}
			return result;
		}

		private static bool IsUnifiable(Constraint<Word, ShapeNode> constraint, Expression<Word, ShapeNode> env)
		{
			foreach (Constraint<Word, ShapeNode> curConstraint in env.GetNodes().OfType<Constraint<Word, ShapeNode>>())
			{
				if (curConstraint.Type == HCFeatureSystem.SegmentType
					&& !curConstraint.FeatureStruct.IsUnifiable(constraint.FeatureStruct))
				{
					return false;
				}
			}

			return true;
		}

		public override AnalysisReapplyType AnalysisReapplyType
		{
			get { return _reapplyType; }
		}

		public override Annotation<ShapeNode> ApplyRhs(Word input, PatternMatch<ShapeNode> match, out Word output)
		{
			ShapeNode endNode = null;
			int i = 0;
			foreach (Constraint<Word, ShapeNode> constraint in _analysisRhs.Children)
			{
				ShapeNode node = match["target" + i].GetStart(Direction);
				if (endNode == null || node.CompareTo(endNode, Direction) > 0)
					endNode = node;
				node.Annotation.FeatureStruct.Merge(constraint.FeatureStruct, match.VariableBindings);
				i++;
			}

			ShapeNode resumeNode = match.GetStart(Direction).GetNext(Direction);
			MarkSearchedNodes(resumeNode, endNode);

			output = input;
			return resumeNode.Annotation;
		}
	}
}

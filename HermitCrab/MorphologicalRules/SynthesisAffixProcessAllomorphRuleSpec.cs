﻿using System;
using System.Collections.Generic;
using System.Linq;
using SIL.Collections;
using SIL.Machine.Annotations;
using SIL.Machine.Matching;
using SIL.Machine.Rules;

namespace SIL.HermitCrab.MorphologicalRules
{
	public class SynthesisAffixProcessAllomorphRuleSpec : IPatternRuleSpec<Word, ShapeNode>
	{
		private readonly AffixProcessAllomorph _allomorph;
		private readonly Pattern<Word, ShapeNode> _pattern;
		private readonly HashSet<MorphologicalOutputAction> _nonAllomorphActions;

		public SynthesisAffixProcessAllomorphRuleSpec(AffixProcessAllomorph allomorph)
		{
			_allomorph = allomorph;

			IList<Pattern<Word, ShapeNode>> lhs = _allomorph.Lhs;
			IList<MorphologicalOutputAction> rhs = _allomorph.Rhs;
			_nonAllomorphActions = new HashSet<MorphologicalOutputAction>();
			var redupParts = new List<List<MorphologicalOutputAction>>();
			foreach (List<MorphologicalOutputAction> partActions in rhs.Where(action => !string.IsNullOrEmpty(action.PartName))
				.GroupBy(action => action.PartName).Select(g => g.ToList()))
			{
				if (partActions.Count == 1)
				{
					if (partActions[0] is CopyFromInput)
						_nonAllomorphActions.Add(partActions[0]);
				}
				else
				{
					redupParts.Add(partActions);
				}
			}
			if (redupParts.Count > 0)
			{
				int start = -1;
				switch (_allomorph.ReduplicationHint)
				{
					case ReduplicationHint.Prefix:
						int prefixPartIndex = lhs.Count - 1;
						for (int i = rhs.Count - 1; i >= 0; i--)
						{
							MorphologicalOutputAction action = rhs[i];
							if (action.PartName == lhs[prefixPartIndex].Name || action.PartName == lhs[lhs.Count - 1].Name)
							{
								if (action.PartName == lhs[0].Name)
								{
									start = i;
									break;
								}
								if (action.PartName != lhs[prefixPartIndex].Name)
									prefixPartIndex = lhs.Count - 1;
								prefixPartIndex--;
							}
							else
							{
								prefixPartIndex = lhs.Count - 1;
							}
						}
						break;

					case ReduplicationHint.Suffix:
					case ReduplicationHint.Implicit:
						int suffixPartIndex = 0;
						for (int i = 0; i < rhs.Count; i++)
						{
							MorphologicalOutputAction action = rhs[i];
							if (action.PartName == lhs[suffixPartIndex].Name || action.PartName == lhs[0].Name)
							{
								if (action.PartName == lhs[lhs.Count - 1].Name)
								{
									start = i - (lhs.Count - 1);
									break;
								}
								if (action.PartName != lhs[suffixPartIndex].Name)
									suffixPartIndex = 0;
								suffixPartIndex++;
							}
							else
							{
								suffixPartIndex = 0;
							}
						}
						break;
				}

				foreach (List<MorphologicalOutputAction> partActions in redupParts)
				{
					for (int j = 0; j < partActions.Count; j++)
					{
						int index = rhs.IndexOf(partActions[j]);
						if ((start == -1 && j == (_allomorph.ReduplicationHint == ReduplicationHint.Prefix ? partActions.Count - 1 : 0))
							|| (start != -1 && index >= start && index < start + lhs.Count))
						{
							_nonAllomorphActions.Add(partActions[j]);
						}
					}
				}
			}

			_pattern = new Pattern<Word, ShapeNode>();
			foreach (Pattern<Word, ShapeNode> part in lhs)
				_pattern.Children.Add(new Group<Word, ShapeNode>(part.Name, part.Children.DeepClone()));
		}

		public Pattern<Word, ShapeNode> Pattern
		{
			get { return _pattern; }
		}

		public bool IsApplicable(Word input)
		{
			return true;
		}

		public Word ApplyRhs(PatternRule<Word, ShapeNode> rule, Match<Word, ShapeNode> match)
		{
			Word output = match.Input.DeepClone();
			output.Shape.Clear();
			var existingMorphNodes = new Dictionary<Annotation<ShapeNode>, List<ShapeNode>>();
			var newMorphNodes = new List<ShapeNode>();
			foreach (MorphologicalOutputAction outputAction in _allomorph.Rhs)
			{
				foreach (Tuple<ShapeNode, ShapeNode> mapping in outputAction.Apply(match, output))
				{
					if (mapping.Item1 != null && _nonAllomorphActions.Contains(outputAction))
					{
						if (mapping.Item1.Annotation.Parent != null)
						{
							Annotation<ShapeNode> morph = mapping.Item1.Annotation.Parent;
							existingMorphNodes.GetValue(morph, () => new List<ShapeNode>()).Add(mapping.Item2);
						}
					}
					else
					{
						newMorphNodes.Add(mapping.Item2);
					}
				}
			}

			Annotation<ShapeNode> outputNewMorph = MarkMorphs(newMorphNodes, output, _allomorph);

			foreach (Annotation<ShapeNode> inputMorph in match.Input.Morphs)
			{
				Allomorph allomorph = match.Input.GetAllomorph(inputMorph);
				List<ShapeNode> nodes;
				if (existingMorphNodes.TryGetValue(inputMorph, out nodes))
				{
					Annotation<ShapeNode> outputMorph = MarkMorphs(nodes, output, allomorph);
					MarkSubsumedMorphs(match.Input, output, inputMorph, outputMorph);
				}
				else if (inputMorph.Parent == null)
				{
					// an existing morph has been completely subsumed by the new morph
					// mark the subsumed morph so we don't lose track of it
					Annotation<ShapeNode> outputMorph = output.MarkSubsumedMorph(outputNewMorph, allomorph);
					MarkSubsumedMorphs(match.Input, output, inputMorph, outputMorph);
				}
			}

			output.MprFeatures.AddOutput(_allomorph.OutMprFeatures);

			return output;
		}

		private void MarkSubsumedMorphs(Word input, Word output, Annotation<ShapeNode> inputMorph, Annotation<ShapeNode> outputMorph)
		{
			if (inputMorph.IsLeaf)
				return;

			foreach (Annotation<ShapeNode> inputChild in inputMorph.Children.Where(ann => ann.Type() == HCFeatureSystem.Morph))
			{
				Allomorph allomorph = input.GetAllomorph(inputChild);
				Annotation<ShapeNode> outputChild = output.MarkSubsumedMorph(outputMorph, allomorph);
				MarkSubsumedMorphs(input, output, inputChild, outputChild);
			}
		}

		private Annotation<ShapeNode> MarkMorphs(List<ShapeNode> nodes, Word output, Allomorph allomorph)
		{
			Annotation<ShapeNode> longestMorph = null;
			var curMorphNodes = new List<ShapeNode>();
			for (int i = 0; i < nodes.Count; i++)
			{
				curMorphNodes.Add(nodes[i]);
				// only contiguous nodes should be marked as a morph
				if (i == nodes.Count - 1 || nodes[i].Next != nodes[i + 1])
				{
					Annotation<ShapeNode> morph = output.MarkMorph(curMorphNodes, allomorph);
					if (longestMorph == null || morph.Span.Length > longestMorph.Span.Length)
						longestMorph = morph;
					curMorphNodes.Clear();
				}
			}
			return longestMorph;
		}
	}
}

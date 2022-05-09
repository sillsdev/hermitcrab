using System;
using System.Collections.Generic;
using System.Linq;
using SIL.Collections;
using SIL.Machine.Annotations;
using SIL.Machine.FeatureModel;

namespace SIL.HermitCrab
{
	public class Word : Freezable<Word>, IAnnotatedData<ShapeNode>, IDeepCloneable<Word>
	{
		public const string RootMorphID = "ROOT";

		private readonly Dictionary<string, Allomorph> _allomorphs;
		private RootAllomorph _rootAllomorph;
		private Shape _shape;
		private readonly Stack<IMorphologicalRule> _mrules;
		private readonly Dictionary<IMorphologicalRule, int> _mrulesUnapplied;
		private readonly Dictionary<IMorphologicalRule, int> _mrulesApplied;
		private readonly Stack<Word> _nonHeads;
		private readonly MprFeatureSet _mprFeatures;
		private readonly IDBearerSet<Feature> _obligatorySyntacticFeatures;
		private FeatureStruct _realizationalFS;
		private Stratum _stratum;
		private bool? _isLastAppliedRuleFinal;
		private bool _isPartial;
		private readonly Dictionary<string, HashSet<int>> _disjunctiveAllomorphIndices;
		private int _mruleAppCount = 0;

		public Word(RootAllomorph rootAllomorph, FeatureStruct realizationalFS)
		{
			_allomorphs = new Dictionary<string, Allomorph>();
			_mprFeatures = new MprFeatureSet();
			_shape = rootAllomorph.Segments.Shape.DeepClone();
			ResetDirty();
			SetRootAllomorph(rootAllomorph);
			RealizationalFeatureStruct = realizationalFS;
			_mrules = new Stack<IMorphologicalRule>();
			_mrulesUnapplied = new Dictionary<IMorphologicalRule, int>();
			_mrulesApplied = new Dictionary<IMorphologicalRule, int>();
			_nonHeads = new Stack<Word>();
			_obligatorySyntacticFeatures = new IDBearerSet<Feature>();
			_isLastAppliedRuleFinal = null;
			_disjunctiveAllomorphIndices = new Dictionary<string, HashSet<int>>();
		}

		public Word(Stratum stratum, Shape shape)
		{
			_allomorphs = new Dictionary<string, Allomorph>();
			Stratum = stratum;
			_shape = shape;
			ResetDirty();
			SyntacticFeatureStruct = new FeatureStruct();
			RealizationalFeatureStruct = new FeatureStruct();
			_mprFeatures = new MprFeatureSet();
			_mrules = new Stack<IMorphologicalRule>();
			_mrulesUnapplied = new Dictionary<IMorphologicalRule, int>();
			_mrulesApplied = new Dictionary<IMorphologicalRule, int>();
			_nonHeads = new Stack<Word>();
			_obligatorySyntacticFeatures = new IDBearerSet<Feature>();
			_isLastAppliedRuleFinal = null;
			_isPartial = false;
			_disjunctiveAllomorphIndices = new Dictionary<string, HashSet<int>>();
		}

		protected Word(Word word)
		{
			_allomorphs = new Dictionary<string, Allomorph>(word._allomorphs);
			Stratum = word.Stratum;
			_shape = word._shape.DeepClone();
			_rootAllomorph = word._rootAllomorph;
			SyntacticFeatureStruct = word.SyntacticFeatureStruct.DeepClone();
			RealizationalFeatureStruct = word.RealizationalFeatureStruct.DeepClone();
			_mprFeatures = word.MprFeatures.DeepClone();
			_mrules = new Stack<IMorphologicalRule>(word._mrules.Reverse());
			_mrulesUnapplied = new Dictionary<IMorphologicalRule, int>(word._mrulesUnapplied);
			_mrulesApplied = new Dictionary<IMorphologicalRule, int>(word._mrulesApplied);
			_nonHeads = new Stack<Word>(word._nonHeads.Reverse().DeepClone());
			_obligatorySyntacticFeatures = new IDBearerSet<Feature>(word._obligatorySyntacticFeatures);
			_isLastAppliedRuleFinal = word._isLastAppliedRuleFinal;
			_isPartial = word._isPartial;
			CurrentTrace = word.CurrentTrace;
			_disjunctiveAllomorphIndices = word._disjunctiveAllomorphIndices.ToDictionary(kvp => kvp.Key, kvp => new HashSet<int>(kvp.Value));
			_mruleAppCount = word._mruleAppCount;
		}

		public IEnumerable<Annotation<ShapeNode>> Morphs
		{
			get
			{
				var morphs = new List<Annotation<ShapeNode>>();
				foreach (Annotation<ShapeNode> ann in Annotations)
				{
					ann.PostorderTraverse(a =>
					{
						if (a.Type() == HCFeatureSystem.Morph)
							morphs.Add(a);
					});
				}
				return morphs;
			}
		}

		public IEnumerable<Allomorph> AllomorphsInMorphOrder
		{
			get
			{
				// there can be multiple morphs for a single allomorph, but we only want to return an allomorph on its
				// first occurrence, so we use distinct
				return Morphs.Select(GetAllomorph).Distinct();
			}
		}

		public ICollection<Allomorph> Allomorphs
		{
			get { return _allomorphs.Values; }
		}

		public RootAllomorph RootAllomorph
		{
			get { return _rootAllomorph; }

			internal set
			{
				CheckFrozen();
				_shape = value.Segments.Shape.DeepClone();
				SetRootAllomorph(value);
			}
		}

		private void SetRootAllomorph(RootAllomorph rootAllomorph)
		{
			_rootAllomorph = rootAllomorph;
			var entry = (LexEntry)_rootAllomorph.Morpheme;
			Stratum = entry.Stratum;
			MarkMorph(_shape, _rootAllomorph, RootMorphID);
			SyntacticFeatureStruct = entry.SyntacticFeatureStruct.DeepClone();
			_mprFeatures.Clear();
			_mprFeatures.UnionWith(entry.MprFeatures);
			_isPartial = entry.IsPartial;
		}

		public Shape Shape
		{
			get { return _shape; }
		}

		public FeatureStruct SyntacticFeatureStruct { get; internal set; }

		public FeatureStruct RealizationalFeatureStruct
		{
			get { return _realizationalFS; }
			internal set
			{
				CheckFrozen();
				_realizationalFS = value;
			}
		}

		public MprFeatureSet MprFeatures
		{
			get { return _mprFeatures; }
		}

		public ICollection<Feature> ObligatorySyntacticFeatures
		{
			get { return _obligatorySyntacticFeatures; }
		}

		public Span<ShapeNode> Span
		{
			get { return _shape.Span; }
		}

		public AnnotationList<ShapeNode> Annotations
		{
			get { return _shape.Annotations; }
		}

		public Stratum Stratum
		{
			get { return _stratum; }
			internal set
			{
				CheckFrozen();
				_stratum = value;
			}
		}

		public object CurrentTrace { get; set; }

		public bool IsPartial
		{
			get { return _isPartial; }
			internal set
			{
				CheckFrozen();
				_isPartial = value;
			}
		}

		internal int MorphologicalRuleApplicationCount => _mruleAppCount;

		public IEnumerable<IMorphologicalRule> MorphologicalRules
		{
			get { return _mrules; }
		}

		/// <summary>
		/// Gets the current rule.
		/// </summary>
		/// <value>The current rule.</value>
		internal IMorphologicalRule CurrentMorphologicalRule
		{
			get
			{
				if (_mrules.Count == 0)
					return null;
				return _mrules.Peek();
			}
		}

		internal Annotation<ShapeNode> MarkMorph(IEnumerable<ShapeNode> nodes, Allomorph allomorph, string morphID)
		{
			ShapeNode[] nodeArray = nodes.ToArray();
			Annotation<ShapeNode> ann = null;
			if (nodeArray.Length > 0)
			{
				ann = new Annotation<ShapeNode>(_shape.SpanFactory.Create(nodeArray[0], nodeArray[nodeArray.Length - 1]), FeatureStruct.New()
					.Symbol(HCFeatureSystem.Morph)
					.Feature(HCFeatureSystem.Allomorph).EqualTo(allomorph.ID)
					.Feature(HCFeatureSystem.MorphID).EqualTo(morphID).Value);
				ann.Children.AddRange(nodeArray.Select(n => n.Annotation));
				_shape.Annotations.Add(ann, false);
			}
			_allomorphs[allomorph.ID] = allomorph;
			return ann;
		}

		internal Annotation<ShapeNode> MarkSubsumedMorph(Annotation<ShapeNode> morph, Allomorph allomorph, string morphID)
		{
			Annotation<ShapeNode> ann = new Annotation<ShapeNode>(morph.Span, FeatureStruct.New()
				.Symbol(HCFeatureSystem.Morph)
				.Feature(HCFeatureSystem.Allomorph).EqualTo(allomorph.ID)
				.Feature(HCFeatureSystem.MorphID).EqualTo(morphID).Value);
			morph.Children.Add(ann, false);
			_allomorphs[allomorph.ID] = allomorph;
			return ann;
		}

		internal void RemoveMorph(Annotation<ShapeNode> morphAnn)
		{
			var alloID = (string)morphAnn.FeatureStruct.GetValue(HCFeatureSystem.Allomorph);
			_allomorphs.Remove(alloID);
			foreach (ShapeNode node in _shape.GetNodes(morphAnn.Span).ToArray())
				node.Remove();
		}

		/// <summary>
		/// Notifies this analysis that the specified morphological rule was unapplied.
		/// </summary>
		/// <param name="mrule">The morphological rule.</param>
		/// <param name="realizational"> </param>
		internal void MorphologicalRuleUnapplied(IMorphologicalRule mrule, bool realizational)
		{
			CheckFrozen();
			_mrulesUnapplied.UpdateValue(mrule, () => 0, count => count + 1);
			if (!realizational)
				_mrules.Push(mrule);
		}

		/// <summary>
		/// Gets the number of times the specified morphological rule has been unapplied.
		/// </summary>
		/// <param name="mrule">The morphological rule.</param>
		/// <returns>The number of unapplications.</returns>
		internal int GetUnapplicationCount(IMorphologicalRule mrule)
		{
			if (!_mrulesUnapplied.TryGetValue(mrule, out int numUnapplies))
				numUnapplies = 0;
			return numUnapplies;
		}

		/// <summary>
		/// Notifies this word synthesis that the specified morphological rule has applied.
		/// </summary>
		internal void MorphologicalRuleApplied(IMorphologicalRule mrule, IEnumerable<int> allomorphIndices = null)
		{
			CheckFrozen();
			_mrulesApplied.UpdateValue(mrule, () => 0, count => count + 1);
			if (allomorphIndices != null)
			{
				if (!_disjunctiveAllomorphIndices.TryGetValue(_mruleAppCount.ToString(), out HashSet<int> indices))
				{
					indices = new HashSet<int>();
					_disjunctiveAllomorphIndices[_mruleAppCount.ToString()] = indices;
				}
				indices.UnionWith(allomorphIndices);
			}
			_mruleAppCount++;
		}

		internal void CurrentMorphologicalRuleApplied(IEnumerable<int> allomorphIndices = null)
		{
			CheckFrozen();
			IMorphologicalRule mrule = _mrules.Pop();
			MorphologicalRuleApplied(mrule, allomorphIndices);
		}

		internal bool? IsLastAppliedRuleFinal
		{
			get { return _isLastAppliedRuleFinal; }
			set
			{
				CheckFrozen();
				_isLastAppliedRuleFinal = value;
			}
		}

		/// <summary>
		/// Gets the number of times the specified morphological rule has been applied.
		/// </summary>
		/// <param name="mrule">The morphological rule.</param>
		/// <returns>The number of applications.</returns>
		internal int GetApplicationCount(IMorphologicalRule mrule)
		{
			if (!_mrulesApplied.TryGetValue(mrule, out int numApplies))
				numApplies = 0;
			return numApplies;
		}

		public Allomorph GetAllomorph(Annotation<ShapeNode> morph)
		{
			var alloID = (string)morph.FeatureStruct.GetValue(HCFeatureSystem.Allomorph);
			return _allomorphs[alloID];
		}

		internal IEnumerable<Annotation<ShapeNode>> GetMorphs(Allomorph allomorph)
		{
			return Morphs.Where(m => (string)m.FeatureStruct.GetValue(HCFeatureSystem.Allomorph) == allomorph.ID);
		}

		internal IEnumerable<int> GetDisjunctiveAllomorphApplications(Annotation<ShapeNode> morph)
		{
			var morphID = (string)morph.FeatureStruct.GetValue(HCFeatureSystem.MorphID);
			if (_disjunctiveAllomorphIndices.TryGetValue(morphID, out HashSet<int> indices))
				return indices;
			return null;
		}

		internal Word CurrentNonHead
		{
			get
			{
				if (_nonHeads.Count == 0)
					return null;
				return _nonHeads.Peek();
			}
		}

		internal int NonHeadCount
		{
			get { return _nonHeads.Count; }
		}

		public void NonHeadUnapplied(Word nonHead)
		{
			CheckFrozen();
			_nonHeads.Push(nonHead);
		}

		internal void CurrentNonHeadApplied()
		{
			CheckFrozen();
			_nonHeads.Pop();
		}

		internal bool CheckBlocking(out Word word)
		{
			word = null;
			LexFamily family = ((LexEntry)RootAllomorph.Morpheme).Family;
			if (family == null)
				return false;

			foreach (LexEntry entry in family.Entries)
			{
				if (entry != RootAllomorph.Morpheme && entry.Stratum == Stratum && SyntacticFeatureStruct.Subsumes(entry.SyntacticFeatureStruct))
				{
					word = new Word(entry.PrimaryAllomorph, RealizationalFeatureStruct.DeepClone()) { CurrentTrace = CurrentTrace };
					word.Freeze();
					return true;
				}
			}

			return false;
		}

		internal void ResetDirty()
		{
			CheckFrozen();
			foreach (ShapeNode node in _shape)
				node.SetDirty(false);
		}

		internal IDictionary<int, Tuple<FailureReason, object>> CurrentRuleResults { get; set; }

		protected override int FreezeImpl()
		{
			int code = 23;
			_shape.Freeze();
			code = code * 31 + _shape.GetFrozenHashCode();
			_realizationalFS.Freeze();
			code = code * 31 + _realizationalFS.GetFrozenHashCode();
			foreach (Word nonHead in _nonHeads)
			{
				nonHead.Freeze();
				code = code * 31 + nonHead.GetFrozenHashCode();
			}
			code = code * 31 + _stratum.GetHashCode();
			code = code * 31 + (_rootAllomorph == null ? 0 : _rootAllomorph.GetHashCode());
			code = code * 31 + _mrules.GetSequenceHashCode();
			code = code * 31 + _isLastAppliedRuleFinal.GetHashCode();
			return code;
		}

		public override bool ValueEquals(Word other)
		{
			if (other == null)
				return false;

			if (IsFrozen && other.IsFrozen && GetFrozenHashCode() != other.GetFrozenHashCode())
				return false;

			return _shape.ValueEquals(other._shape) && _realizationalFS.ValueEquals(other._realizationalFS)
				&& _nonHeads.SequenceEqual(other._nonHeads, FreezableEqualityComparer<Word>.Default) && _stratum == other._stratum
				&& _rootAllomorph == other._rootAllomorph && _mrules.SequenceEqual(other._mrules) && _isLastAppliedRuleFinal == other._isLastAppliedRuleFinal;
		}

		public Word DeepClone()
		{
			return new Word(this);
		}

		public override string ToString()
		{
			return Shape.ToRegexString(Stratum.CharacterDefinitionTable, true);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using SIL.Collections;
using SIL.Machine.Annotations;
using SIL.Machine.FeatureModel;
using SIL.Machine.Rules;

namespace SIL.HermitCrab
{
	/// <summary>
	/// This class acts as the main interface to the morphing capability of HC.NET. It encapsulates
	/// the feature systems, rules, character definition tables, etc. for a particular language.
	/// </summary>
	public class Language : IDBearerBase, IHCRule
	{
		private readonly ObservableCollection<Stratum> _strata;

		/// <summary>
		/// Initializes a new instance of the <see cref="Language"/> class.
		/// </summary>
		/// <param name="id">The id.</param>
		public Language(string id)
			: base(id)
		{
			_strata = new ObservableCollection<Stratum>();
			_strata.CollectionChanged += StrataChanged;
			PhoneticFeatureSystem = new FeatureSystem();
			SyntacticFeatureSystem = new FeatureSystem();
		}

		private void StrataChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
			{
				foreach (Stratum stratum in e.OldItems)
					stratum.Depth = -1;
			}
			for (int i = 0; i < _strata.Count; i++)
				_strata[i].Depth = i;
		}

		/// <summary>
		/// Gets the surface stratum.
		/// </summary>
		/// <value>The surface stratum.</value>
		public Stratum SurfaceStratum
		{
			get
			{
				if (_strata.Count == 0)
					return null;
				return _strata[_strata.Count - 1];
			}
		}

		/// <summary>
		/// Gets the phonetic feature system.
		/// </summary>
		/// <value>The phonetic feature system.</value>
		public FeatureSystem PhoneticFeatureSystem { get; set; }

		/// <summary>
		/// Gets the syntactic feature system.
		/// </summary>
		/// <value>The syntactic feature system.</value>
		public FeatureSystem SyntacticFeatureSystem { get; set; }

		/// <summary>
		/// Gets all strata, including the surface stratum.
		/// </summary>
		/// <value>The strata.</value>
		public IList<Stratum> Strata
		{
			get { return _strata; }
		}

		public IRule<Word, ShapeNode> CompileAnalysisRule(SpanFactory<ShapeNode> spanFactory, Morpher morpher)
		{
			//return new PermutationRuleCascade<Word, ShapeNode>(_strata.Select(stratum => stratum.CompileAnalysisRule(spanFactory, morpher)).Reverse(),
			//	FreezableEqualityComparer<Word>.Instance);
			return new AnalysisLanguageRule(spanFactory, morpher, _strata);
		}

		public IRule<Word, ShapeNode> CompileSynthesisRule(SpanFactory<ShapeNode> spanFactory, Morpher morpher)
		{
			return new PipelineRuleCascade<Word, ShapeNode>(_strata.Select(stratum => stratum.CompileSynthesisRule(spanFactory, morpher)),
				FreezableEqualityComparer<Word>.Default);
		}

		public void Traverse(Action<IHCRule> action)
		{
			action(this);
			foreach (Stratum stratum in _strata)
				stratum.Traverse(action);
		}

		private class AnalysisLanguageRule : IRule<Word, ShapeNode>
		{
			private readonly List<IRule<Word, ShapeNode>> _rules; 

			public AnalysisLanguageRule(SpanFactory<ShapeNode> spanFactory, Morpher morpher, IEnumerable<Stratum> strata)
			{
				_rules = strata.Select(stratum => stratum.CompileAnalysisRule(spanFactory, morpher)).Reverse().ToList();
			}

			public IEnumerable<Word> Apply(Word input)
			{
				var inputSet = new HashSet<Word>(FreezableEqualityComparer<Word>.Default){input};
				var tempSet = new HashSet<Word>(FreezableEqualityComparer<Word>.Default);
				var results = new HashSet<Word>(FreezableEqualityComparer<Word>.Default);
				for (int i = 0; i < _rules.Count && inputSet.Count > 0; i++)
				{
					HashSet<Word> outputSet = tempSet;
					outputSet.Clear();

					foreach (Word inData in inputSet)
					{
						foreach (Word outData in _rules[i].Apply(inData))
						{
							outputSet.Add(outData);
							results.Add(outData);
						}
					}

					tempSet = inputSet;
					inputSet = outputSet;
				}

				return results;
			}
		}
	}
}
﻿using System;
#if !SINGLE_THREADED
using System.Collections.Concurrent;
using System.Threading.Tasks;
#endif
using System.Collections.Generic;
#if OUTPUT_ANALYSES
using System.IO;
#endif
using System.Linq;
using SIL.Collections;
using SIL.Machine.Annotations;
using SIL.Machine.FeatureModel;
using SIL.Machine.Rules;

namespace SIL.HermitCrab
{
	public class Morpher
	{
		private static readonly IEqualityComparer<IEnumerable<Allomorph>> MorphsEqualityComparer = SequenceEqualityComparer.Create(ProjectionEqualityComparer<Allomorph>.Create(allo => allo.Morpheme));

		private readonly Language _lang;
		private readonly IRule<Word, ShapeNode> _analysisRule;
		private readonly IRule<Word, ShapeNode> _synthesisRule;
		private readonly Dictionary<Stratum, RootAllomorphTrie> _allomorphTries;
		private readonly ITraceManager _traceManager;

		public Morpher(SpanFactory<ShapeNode> spanFactory, ITraceManager traceManager, Language lang)
		{
			_lang = lang;
			_traceManager = traceManager;
			_allomorphTries = new Dictionary<Stratum, RootAllomorphTrie>();
			foreach (Stratum stratum in _lang.Strata)
			{
				var allomorphs = new HashSet<RootAllomorph>(stratum.Entries.SelectMany(entry => entry.Allomorphs));
				var trie = new RootAllomorphTrie(ann => ann.Type() == HCFeatureSystem.Segment);
				foreach (RootAllomorph allomorph in allomorphs)
					trie.Add(allomorph);
				_allomorphTries[stratum] = trie;
			}
			_analysisRule = lang.CompileAnalysisRule(spanFactory, this);
			_synthesisRule = lang.CompileSynthesisRule(spanFactory, this);
			MaxStemCount = 2;
			LexEntrySelector = entry => true;
			RuleSelector = rule => true;
		}

		public ITraceManager TraceManager
		{
			get { return _traceManager; }
		}

		public int DeletionReapplications { get; set; }

		public int MaxStemCount { get; set; }

		public Func<LexEntry, bool> LexEntrySelector { get; set; }
		public Func<IHCRule, bool> RuleSelector { get; set; }

		public Language Language
		{
			get { return _lang; }
		}

		/// <summary>
		/// Morphs the specified word.
		/// </summary>
		/// <param name="word">The word.</param>
		/// <returns>All valid word synthesis records.</returns>
		public IEnumerable<Word> ParseWord(string word)
		{
			object trace;
			return ParseWord(word, out trace);
		}

		public IEnumerable<Word> ParseWord(string word, out object trace)
		{
			// convert the word to its phonetic shape
			Shape shape = _lang.SurfaceStratum.CharacterDefinitionTable.Segment(word);

			var input = new Word(_lang.SurfaceStratum, shape);
			input.Freeze();
			if (_traceManager.IsTracing)
				_traceManager.AnalyzeWord(_lang, input);
			trace = input.CurrentTrace;

			// Unapply rules
			IEnumerable<Word> analyses = _analysisRule.Apply(input);

#if OUTPUT_ANALYSES
			var lines = new List<string>();
			foreach (Word w in analyses)
			{
				string shapeStr = w.ToString();
				string rulesStr = string.Join(", ", w.MorphologicalRules.Select(r => r.Name));
				lines.Add(string.Format("{0} : {1}", shapeStr, rulesStr));
			}

			File.WriteAllLines("analyses.txt", lines.OrderBy(l => l));
#endif

#if SINGLE_THREADED
			IEnumerable<Word> validWords = Synthesize(analyses);
#else
			IEnumerable<Word> validWords = ParallelSynthesize(analyses);
#endif

			var matchList = new List<Word>();
			foreach (IGrouping<IEnumerable<Allomorph>, Word> group in validWords.GroupBy(validWord => validWord.AllomorphsInMorphOrder, MorphsEqualityComparer))
			{
				// enforce the disjunctive property of allomorphs by ensuring that this word synthesis
				// has the highest order of precedence for its allomorphs
				Word[] words = group.ToArray();
				for (int i = 0; i < words.Length; i++)
				{
					bool disjunctive = false;
					for (int j = 0; j < words.Length; j++)
					{
						if (i == j)
							continue;

						// if the two parses differ by one allomorph and that allomorph does not free fluctuate and has a lower precedence, than the parse fails
						Tuple<Allomorph, Allomorph>[] differentAllomorphs = words[i].AllomorphsInMorphOrder.Zip(words[j].AllomorphsInMorphOrder).Where(t => t.Item1 != t.Item2).ToArray();
						if (differentAllomorphs.Length == 1 && !differentAllomorphs[0].Item1.FreeFluctuatesWith(differentAllomorphs[0].Item2)
							&& differentAllomorphs[0].Item1.Index >= differentAllomorphs[0].Item2.Index)
						{
							disjunctive = true;
							if (_traceManager.IsTracing)
								_traceManager.ParseFailed(_lang, words[i], FailureReason.DisjunctiveAllomorph, null, words[j]);
							break;
						}
					}

					if (!disjunctive)
					{
						if (_lang.SurfaceStratum.CharacterDefinitionTable.IsMatch(word, words[i].Shape))
						{
							if (_traceManager.IsTracing)
								_traceManager.ParseSuccessful(_lang, words[i]);
							matchList.Add(words[i]);
						}
						else if (_traceManager.IsTracing)
						{
							_traceManager.ParseFailed(_lang, words[i], FailureReason.SurfaceFormMismatch, null, word);
						}
					}
				}
			}
			return matchList;
		}

#if SINGLE_THREADED
		private IEnumerable<Word> Synthesize(IEnumerable<Word> analyses)
		{
			var validWords = new HashSet<Word>(FreezableEqualityComparer<Word>.Default);
			foreach (Word analysisWord in analyses)
			{
				foreach (Word synthesisWord in LexicalLookup(analysisWord))
					validWords.UnionWith(_synthesisRule.Apply(synthesisWord).Where(IsWordValid));
			}
			return validWords;
		}
#else
		private IEnumerable<Word> ParallelSynthesize(IEnumerable<Word> analyses)
		{
			var validWordsStack = new ConcurrentStack<Word>();
			Exception exception = null;
			Parallel.ForEach(analyses, (analysisWord, state) =>
			{
				try
				{
					foreach (Word synthesisWord in LexicalLookup(analysisWord))
					{
						Word[] valid = _synthesisRule.Apply(synthesisWord).Where(IsWordValid).ToArray();
						if (valid.Length > 0)
							validWordsStack.PushRange(valid);
					}
				}
				catch (Exception e)
				{
					state.Stop();
					exception = e;
				}
			});

			if (exception != null)
				throw exception;
			return validWordsStack.Distinct(FreezableEqualityComparer<Word>.Default);
		}
#endif

		internal IEnumerable<RootAllomorph> SearchRootAllomorphs(Stratum stratum, Shape shape)
		{
			RootAllomorphTrie alloSearcher = _allomorphTries[stratum];
			return alloSearcher.Search(shape).Distinct();
		}

		private IEnumerable<Word> LexicalLookup(Word input)
		{
			if (input.ToString().Length == 0)
				yield break;
			if (_traceManager.IsTracing)
				_traceManager.LexicalLookup(input.Stratum, input);
			foreach (LexEntry entry in SearchRootAllomorphs(input.Stratum, input.Shape).Select(allo => allo.Morpheme).Cast<LexEntry>().Where(LexEntrySelector).Distinct())
			{
				foreach (RootAllomorph allomorph in entry.Allomorphs)
				{
					Word newWord = input.DeepClone();
					newWord.RootAllomorph = allomorph;
					if (_traceManager.IsTracing)
						_traceManager.SynthesizeWord(_lang, newWord);
					newWord.Freeze();
					yield return newWord;
				}
			}
		}

		private bool IsWordValid(Word word)
		{
			if (!word.RealizationalFeatureStruct.IsUnifiable(word.SyntacticFeatureStruct) || word.CurrentMorphologicalRule != null)
			{
				if (_traceManager.IsTracing)
					_traceManager.ParseFailed(_lang, word, FailureReason.PartialParse, null, null);
				return false;
			}

			Feature feature = word.ObligatorySyntacticFeatures.FirstOrDefault(f => !ContainsFeature(word.SyntacticFeatureStruct, f, new HashSet<FeatureStruct>(new ReferenceEqualityComparer<FeatureStruct>())));
			if (feature != null)
			{
				if (_traceManager.IsTracing)
					_traceManager.ParseFailed(_lang, word, FailureReason.ObligatorySyntacticFeatures, null, feature);
				return false;
			}

			return word.Allomorphs.All(allo => allo.IsWordValid(this, word));
		}

		private bool ContainsFeature(FeatureStruct fs, Feature feature, ISet<FeatureStruct> visited)
		{
			if (visited.Contains(fs))
				return false;

			if (fs.ContainsFeature(feature))
				return true;

			if (fs.Features.OfType<ComplexFeature>().Any(cf => ContainsFeature(fs.GetValue(cf), feature, visited)))
				return true;

			return false;
		}
	}
}

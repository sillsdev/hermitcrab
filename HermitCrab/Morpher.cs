using System;
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
			return ParseWord(word, out _);
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
			var analyses = new ConcurrentQueue<Word>(_analysisRule.Apply(input));

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

			return Synthesize(word, analyses);
		}

#if SINGLE_THREADED
		private IEnumerable<Word> Synthesize(string word, IEnumerable<Word> analyses)
		{
			var matches = new HashSet<Word>(FreezableEqualityComparer<Word>.Default);
			foreach (Word analysisWord in analyses)
			{
				foreach (Word synthesisWord in LexicalLookup(analysisWord))
				{
					foreach (Word validWord in _synthesisRule.Apply(synthesisWord).Where(IsWordValid))
					{
						if (IsMatch(word, validWord))
							matches.Add(validWord);
					}
				}
			}
			return matches;
		}
#else
		private IEnumerable<Word> Synthesize(string word, ConcurrentQueue<Word> analyses)
		{
			var matches = new ConcurrentBag<Word>();
			Exception exception = null;
			Parallel.ForEach(Partitioner.Create(0, analyses.Count),
				new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
				(range, state) =>
				{
					try
					{
						for (int i = 0; i < range.Item2 - range.Item1; i++)
						{
							analyses.TryDequeue(out Word analysisWord);
							foreach (Word synthesisWord in LexicalLookup(analysisWord))
							{
								foreach (Word validWord in _synthesisRule.Apply(synthesisWord).Where(IsWordValid))
								{
									if (IsMatch(word, validWord))
										matches.Add(validWord);
								}
							}
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
			return matches.Distinct(FreezableEqualityComparer<Word>.Default);
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

		private bool IsMatch(string word, Word validWord)
		{
			if (_lang.SurfaceStratum.CharacterDefinitionTable.IsMatch(word, validWord.Shape))
			{
				if (_traceManager.IsTracing)
					_traceManager.ParseSuccessful(_lang, validWord);
				return true;
			}
			else if (_traceManager.IsTracing)
			{
				_traceManager.ParseFailed(_lang, validWord, FailureReason.SurfaceFormMismatch, null, word);
			}
			return false;
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

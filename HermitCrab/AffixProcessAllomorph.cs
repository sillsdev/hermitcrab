﻿using System.Collections.Generic;
using SIL.APRE.Matching;

namespace SIL.HermitCrab
{
	public class AffixProcessAllomorph : Allomorph
	{
		private readonly List<Expression<Word, ShapeNode>> _lhs;
		private readonly List<MorphologicalOutput> _rhs;

		public AffixProcessAllomorph(string id)
			: base(id)
		{
			_lhs = new List<Expression<Word, ShapeNode>>();
			_rhs = new List<MorphologicalOutput>();
		}

		public IList<Expression<Word, ShapeNode>> Lhs
		{
			get { return _lhs; }
		}

		public IList<MorphologicalOutput> Rhs
		{
			get { return _rhs; }
		}

	}
}
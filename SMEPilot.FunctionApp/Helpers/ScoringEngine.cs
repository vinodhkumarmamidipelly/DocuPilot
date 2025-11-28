using System;
using System.Collections.Generic;
using System.Linq;
using SMEPilot.FunctionApp.Helpers;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Step 6: ScoringEngine
    /// Purpose: Convert features → deterministic score
    /// Prevents: Wrong data inserted, randomness in results, non-reproducible behavior
    /// </summary>
    public class ScoringEngine
    {
        /// <summary>
        /// Default weights for feature components
        /// </summary>
        public class Weights
        {
            public double Alias { get; set; } = 0.5;
            public double NormalizedExact { get; set; } = 0.2;
            public double Fuzzy { get; set; } = 0.15;
            public double Pattern { get; set; } = 0.1;
            public double Context { get; set; } = 0.05;
        }

        private readonly Weights _weights;

        public ScoringEngine(Weights? weights = null)
        {
            _weights = weights ?? new Weights();
        }

        /// <summary>
        /// Computes score and provenance for a feature set
        /// </summary>
        public ScoreResult Score(SignalGenerator.FeatureSet features)
        {
            var score = 0.0;
            var provenance = new ScoreProvenance();

            // Alias hit (highest priority)
            if (features.AliasHit)
            {
                score += _weights.Alias;
                provenance.Alias = _weights.Alias;
            }

            // Normalized exact match
            if (features.NormalizedExact)
            {
                score += _weights.NormalizedExact;
                provenance.NormalizedExact = _weights.NormalizedExact;
            }

            // Fuzzy similarity (0-100 scaled to weight)
            var fuzzyContribution = (features.Fuzzy / 100.0) * _weights.Fuzzy;
            score += fuzzyContribution;
            provenance.Fuzzy = fuzzyContribution;

            // Pattern match
            if (features.PatternMatch)
            {
                score += _weights.Pattern;
                provenance.Pattern = _weights.Pattern;
            }

            // Context match
            if (features.ContextMatch)
            {
                score += _weights.Context;
                provenance.Context = _weights.Context;
            }

            // Position bonus (earlier paragraphs get slight boost for header fields)
            var positionBonus = Math.Max(0, (100 - features.ParagraphIndex) / 1000.0) * 0.02;
            score += positionBonus;

            // Clamp to [0, 1]
            score = Math.Min(1.0, Math.Max(0.0, score));

            return new ScoreResult
            {
                Score = score,
                Provenance = provenance,
                Features = features
            };
        }

        /// <summary>
        /// Finds best candidate from multiple feature sets
        /// </summary>
        public ScoreResult? FindBestCandidate(List<SignalGenerator.FeatureSet> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            var scored = candidates.Select(c => Score(c)).ToList();

            // Sort by score descending, then by paragraph index ascending (earlier = better)
            var best = scored
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.Features.ParagraphIndex)
                .FirstOrDefault();

            return best;
        }

        /// <summary>
        /// Applies deterministic tie-breakers
        /// Priority: alias > pattern > context > fuzzy > position
        /// </summary>
        public ScoreResult? ResolveTie(List<ScoreResult> tiedResults)
        {
            if (tiedResults == null || tiedResults.Count == 0)
                return null;

            if (tiedResults.Count == 1)
                return tiedResults[0];

            // Tie-breaker 1: Alias hit
            var withAlias = tiedResults.Where(r => r.Features.AliasHit).ToList();
            if (withAlias.Count > 0)
            {
                tiedResults = withAlias;
                if (tiedResults.Count == 1)
                    return tiedResults[0];
            }

            // Tie-breaker 2: Pattern match
            var withPattern = tiedResults.Where(r => r.Features.PatternMatch).ToList();
            if (withPattern.Count > 0)
            {
                tiedResults = withPattern;
                if (tiedResults.Count == 1)
                    return tiedResults[0];
            }

            // Tie-breaker 3: Context match
            var withContext = tiedResults.Where(r => r.Features.ContextMatch).ToList();
            if (withContext.Count > 0)
            {
                tiedResults = withContext;
                if (tiedResults.Count == 1)
                    return tiedResults[0];
            }

            // Tie-breaker 4: Higher fuzzy score
            var bestFuzzy = tiedResults.OrderByDescending(r => r.Features.Fuzzy).First();
            return bestFuzzy;
        }

        public class ScoreResult
        {
            public double Score { get; set; }
            public ScoreProvenance Provenance { get; set; } = new();
            public SignalGenerator.FeatureSet Features { get; set; } = new();
        }

        public class ScoreProvenance
        {
            public double Alias { get; set; }
            public double NormalizedExact { get; set; }
            public double Fuzzy { get; set; }
            public double Pattern { get; set; }
            public double Context { get; set; }
        }
    }
}


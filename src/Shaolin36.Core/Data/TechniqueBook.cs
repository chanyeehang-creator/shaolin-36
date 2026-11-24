using System;
using System.Collections.Generic;

namespace Shaolin36.Core.Data
{
    /// <summary>
    /// Every technique the game knows, looked up by id. A fighter uses it to find the next
    /// hit of a string (<see cref="Technique.Next"/>), which sits on no slot of its own.
    /// </summary>
    public sealed class TechniqueBook
    {
        private readonly Dictionary<string, Technique> _byId = new Dictionary<string, Technique>(StringComparer.Ordinal);

        public TechniqueBook(IEnumerable<Technique> techniques)
        {
            if (techniques == null) throw new ArgumentNullException(nameof(techniques));
            foreach (var t in techniques)
            {
                if (_byId.ContainsKey(t.Id)) throw new ArgumentException($"two techniques have the id '{t.Id}'", nameof(techniques));
                _byId[t.Id] = t;
            }
        }

        public int Count => _byId.Count;

        /// <summary>The technique with this id, or null if there is none.</summary>
        public Technique? Find(string id) => _byId.TryGetValue(id, out var t) ? t : null;

        /// <summary>The next hit of <paramref name="technique"/>'s string, or null if it ends the string.</summary>
        public Technique? NextOf(Technique technique)
        {
            if (!technique.HasNext) return null;
            return Find(technique.Next)
                ?? throw new InvalidOperationException($"{technique.Id}: 'next' is '{technique.Next}', but the book has no technique with that id");
        }
    }
}

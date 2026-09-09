// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// The working state of one materialisation call: the indexes built once from the analytical model,
    /// the refusals and notes accumulated so far, the lineage rows, and the derived-identity collision
    /// map.
    /// <para>
    /// <b>Internal, and it is not part of the API.</b> It exists so the entry point and the per-air-handling-unit
    /// pass share one set of indexes rather than rebuilding them - which is the difference between a
    /// linear materialisation and a quadratic one at five thousand spaces.
    /// </para>
    /// </summary>
    internal class MechanicalVentilationContext
    {
        /// <summary>Every space of the model, by guid. Built once; nothing re-enumerates the model.</summary>
        internal Dictionary<Guid, Space> Dictionary_Space { get; } = new Dictionary<Guid, Space>();

        /// <summary>The design terminals of each member space, in ascending terminal guid order.</summary>
        internal Dictionary<Guid, List<VentilationTerminal>> Dictionary_Terminal { get; } = new Dictionary<Guid, List<VentilationTerminal>>();

        /// <summary>Which air handling unit owns each member space, so a space in two groups is caught.</summary>
        internal Dictionary<Guid, Guid> Dictionary_SpaceOwner { get; } = new Dictionary<Guid, Guid>();

        /// <summary>The materialised <c>SystemSpace</c> guid of each member space.</summary>
        internal Dictionary<Guid, Guid> Dictionary_SystemSpaceGuid { get; } = new Dictionary<Guid, Guid>();

        /// <summary>The materialised <c>SystemSpace</c> instance of each member space, for wiring transfers.</summary>
        internal Dictionary<Guid, SystemSpace> Dictionary_SystemSpace { get; } = new Dictionary<Guid, SystemSpace>();

        /// <summary>The materialised <c>AirSystem</c> of each air handling unit.</summary>
        internal Dictionary<Guid, AirSystem> Dictionary_AirSystem { get; } = new Dictionary<Guid, AirSystem>();

        /// <summary>
        /// Whether every design terminal of a unit's systems reached a materialised space - false where
        /// the caller's scope clipped some of them.
        /// <para>
        /// It gates the cross-check against <c>Query.AirHandlingUnitDesignDuty</c>, which sums the unit's
        /// terminals over the whole model and knows nothing about a caller's scope: comparing a scoped
        /// materialisation against it would refuse a perfectly correct partial run.
        /// </para>
        /// </summary>
        internal Dictionary<Guid, bool> Dictionary_ScopeComplete { get; } = new Dictionary<Guid, bool>();

        /// <summary>The design supply duty [l/s] of each member space, absent where it has no supply terminal.</summary>
        internal Dictionary<Guid, double> Dictionary_SupplyDuty_Lps { get; } = new Dictionary<Guid, double>();

        /// <summary>The design extract duty [l/s] of each member space, absent where it has no extract terminal.</summary>
        internal Dictionary<Guid, double> Dictionary_ExtractDuty_Lps { get; } = new Dictionary<Guid, double>();

        /// <summary>The settings of this call - already copied, so the caller cannot reach in.</summary>
        internal MechanicalVentilationSettings Settings { get; set; }

        /// <summary>The derived energy-centre key, which every per-unit key derives through.</summary>
        internal string Key_EnergyCentre { get; set; }

        internal List<string> Refusals { get; } = new List<string>();

        internal List<string> Notes { get; } = new List<string>();

        internal List<MechanicalVentilationBinding> Bindings { get; } = new List<MechanicalVentilationBinding>();

        /// <summary>
        /// Every derived guid against the key text it came from.
        /// <para>
        /// <b>Load-bearing, not theoretical.</b> <c>RelationCluster.TryAddObject</c> silently replaces an
        /// existing entry on a guid collision, so without this map two colliding objects would quietly
        /// become one and the graph would simply be short an object with nothing reported.
        /// </para>
        /// </summary>
        private readonly Dictionary<Guid, string> dictionary_Identity = new Dictionary<Guid, string>();

        internal bool HasRefusals
        {
            get
            {
                return Refusals.Count != 0;
            }
        }

        internal void Refuse(string refusal)
        {
            if (!string.IsNullOrWhiteSpace(refusal))
            {
                Refusals.Add(refusal);
            }
        }

        internal void Note(string note)
        {
            if (!string.IsNullOrWhiteSpace(note))
            {
                Notes.Add(note);
            }
        }

        /// <summary>
        /// Derives one identity and records it, refusing rather than silently overwriting where two
        /// different objects derive the same guid.
        /// </summary>
        internal Guid Guid_Derived(string domain, params string[] components)
        {
            Guid result = Query.MechanicalVentilationGuid(domain, components);

            string key = Key(domain, components);

            if (dictionary_Identity.TryGetValue(result, out string key_Existing))
            {
                if (key_Existing != key)
                {
                    Refuse(string.Format("A deterministic identity collision occurred between '{0}' and '{1}'.", key_Existing, key));
                }

                return result;
            }

            dictionary_Identity[result] = key;

            return result;
        }

        /// <summary>Whether this guid has been derived in this call at all - the finished-graph re-check.</summary>
        internal bool ContainsIdentity(Guid guid)
        {
            return dictionary_Identity.ContainsKey(guid);
        }

        internal int IdentityCount
        {
            get
            {
                return dictionary_Identity.Count;
            }
        }

        private static string Key(string domain, params string[] components)
        {
            return string.Format("{0}[{1}]", domain, components == null ? string.Empty : string.Join("|", components));
        }
    }
}

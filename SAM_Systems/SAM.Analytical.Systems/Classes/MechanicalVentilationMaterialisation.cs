// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Systems
{
    /// <summary>
    /// The whole answer of one mechanical-ventilation materialisation: the isolated graph, why nothing
    /// was materialised where that is the answer, what was materialised, and the complete deterministic
    /// analytical-source lineage.
    /// <para>
    /// <b>There is no partial-success graph.</b> <see cref="SystemEnergyCentre"/> is null whenever
    /// <see cref="Refusals"/> is non-empty, and that is enforced in the constructor rather than left to
    /// convention. A graph and a refusal cannot disagree when one object owns both: a caller that reads
    /// the graph without reading the refusals cannot get a half-built system that states duties the
    /// design does not.
    /// </para>
    /// </summary>
    public class MechanicalVentilationMaterialisation
    {
        private readonly Core.Systems.SystemEnergyCentre systemEnergyCentre;
        private readonly List<string> refusals;
        private readonly List<string> notes;
        private readonly List<MechanicalVentilationBinding> bindings;

        /// <summary>
        /// The isolated materialised energy centre, or <b>null</b> whenever <see cref="Refusals"/> is
        /// non-empty.
        /// </summary>
        public Core.Systems.SystemEnergyCentre SystemEnergyCentre
        {
            get
            {
                return systemEnergyCentre;
            }
        }

        /// <summary>One sentence each, why nothing was materialised. Ordered, and never empty on a refusal.</summary>
        public List<string> Refusals
        {
            get
            {
                return new List<string>(refusals);
            }
        }

        /// <summary>What was materialised. Never a substitute for a refusal.</summary>
        public List<string> Notes
        {
            get
            {
                return new List<string>(notes);
            }
        }

        /// <summary>
        /// The complete deterministic analytical-source lineage: for every materialised object that has an
        /// analytical source, one row per contributing source object. Ordered by
        /// <c>(BindingType, Guid_Analytical, Guid_Analytical_Secondary)</c>, which is unique.
        /// <c>Guid_Systems</c> is <b>not</b> unique - several terminals aggregate into one directional
        /// connection. Purely generated objects - the plant room, and the fans, dampers and junctions
        /// copied from the template - have no analytical source and deliberately do not appear.
        /// </summary>
        public List<MechanicalVentilationBinding> Bindings
        {
            get
            {
                return new List<MechanicalVentilationBinding>(bindings);
            }
        }

        /// <summary>Whether a graph was produced at all.</summary>
        public bool IsMaterialised
        {
            get
            {
                return systemEnergyCentre != null && refusals.Count == 0;
            }
        }

        public MechanicalVentilationMaterialisation(Core.Systems.SystemEnergyCentre systemEnergyCentre, IEnumerable<string> refusals, IEnumerable<string> notes, IEnumerable<MechanicalVentilationBinding> bindings)
        {
            this.refusals = refusals == null ? new List<string>() : new List<string>(refusals);
            this.notes = notes == null ? new List<string>() : new List<string>(notes);
            this.bindings = bindings == null ? new List<MechanicalVentilationBinding>() : new List<MechanicalVentilationBinding>(bindings);

            //Fail closed, structurally. A refusal and a graph cannot both be returned, so nothing
            //downstream has to remember to check the refusals before reading the graph.
            this.systemEnergyCentre = this.refusals.Count == 0 ? systemEnergyCentre : null;

            if (this.refusals.Count != 0)
            {
                this.notes.Clear();
                this.bindings.Clear();
            }
        }
    }
}

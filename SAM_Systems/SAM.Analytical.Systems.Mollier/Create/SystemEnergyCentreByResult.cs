// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using SAM.Analytical.Mollier;
using SAM.Core.Mollier;
using SAM.Core.Systems;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Create
    {
        /// <summary>
        /// Builds a <see cref="SystemEnergyCentre"/> from a Mollier process chain, sourcing the design airflow
        /// from a computed <see cref="AirHandlingUnitResult"/> rather than an explicit value.
        /// </summary>
        /// <remarks>
        /// The supply airflow [m3/s] is read from
        /// <see cref="AirHandlingUnitResultParameter.SupplyAirFlow"/> on the result. This is the "pull from model"
        /// path: the analytical model already knows the design airflow that the intensive Mollier states omit.
        /// </remarks>
        /// <param name="mollierProcesses">Ordered psychrometric process chain.</param>
        /// <param name="airHandlingUnitResult">Computed AHU result carrying the design supply airflow.</param>
        /// <param name="name">Energy centre name.</param>
        /// <returns>A <see cref="SystemEnergyCentre"/>, or null.</returns>
        public static SystemEnergyCentre SystemEnergyCentre(this IEnumerable<IMollierProcess> mollierProcesses, AirHandlingUnitResult airHandlingUnitResult, string name = "Energy Centre")
        {
            List<ConversionDiagnostic> _;
            return SystemEnergyCentre(mollierProcesses, airHandlingUnitResult, name, out _);
        }

        /// <summary>
        /// Builds a <see cref="SAM.Core.Systems.SystemEnergyCentre"/> from a Mollier process chain, sourcing the design airflow
        /// from a computed <see cref="AirHandlingUnitResult"/>, and collects structured diagnostics.
        /// </summary>
        public static SystemEnergyCentre SystemEnergyCentre(this IEnumerable<IMollierProcess> mollierProcesses, AirHandlingUnitResult airHandlingUnitResult, string name, out List<ConversionDiagnostic> diagnostics)
        {
            double designAirflow = DesignAirflow(airHandlingUnitResult);
            return Create.SystemEnergyCentre(mollierProcesses, designAirflow, name, out diagnostics);
        }

        /// <summary>
        /// Builds a <see cref="SystemEnergyCentre"/> from a <see cref="MollierGroup"/>, sourcing the design airflow
        /// from a computed <see cref="AirHandlingUnitResult"/>.
        /// </summary>
        public static SystemEnergyCentre SystemEnergyCentre(this MollierGroup mollierGroup, AirHandlingUnitResult airHandlingUnitResult, string name = null)
        {
            List<ConversionDiagnostic> _;
            return SystemEnergyCentre(mollierGroup, airHandlingUnitResult, name, out _);
        }

        /// <summary>
        /// Builds a <see cref="SAM.Core.Systems.SystemEnergyCentre"/> from a <see cref="MollierGroup"/>, sourcing the design airflow
        /// from a computed <see cref="AirHandlingUnitResult"/>, and collects structured diagnostics.
        /// </summary>
        public static SystemEnergyCentre SystemEnergyCentre(this MollierGroup mollierGroup, AirHandlingUnitResult airHandlingUnitResult, string name, out List<ConversionDiagnostic> diagnostics)
        {
            double designAirflow = DesignAirflow(airHandlingUnitResult);
            return Create.SystemEnergyCentre(mollierGroup, designAirflow, name, out diagnostics);
        }

        private static double DesignAirflow(AirHandlingUnitResult airHandlingUnitResult)
        {
            if (airHandlingUnitResult == null)
            {
                return double.NaN;
            }

            if (airHandlingUnitResult.TryGetValue(AirHandlingUnitResultParameter.SupplyAirFlow, out double supplyAirFlow) && !double.IsNaN(supplyAirFlow))
            {
                return supplyAirFlow;
            }

            return double.NaN;
        }
    }
}

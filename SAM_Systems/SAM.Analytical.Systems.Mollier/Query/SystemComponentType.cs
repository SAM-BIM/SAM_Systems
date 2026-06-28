// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Mollier;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Query
    {
        /// <summary>
        /// Classifies a Mollier process into the SAM_Systems air-handling component type that realises it.
        /// </summary>
        /// <param name="mollierProcess">Psychrometric process drawn on the Mollier diagram.</param>
        /// <returns>The <see cref="System.Type"/> of the matching <see cref="SAM.Core.Systems.ISystemComponent"/>, or null when the process maps to no component.</returns>
        public static System.Type SystemComponentType(this IMollierProcess mollierProcess)
        {
            if (mollierProcess == null)
            {
                return null;
            }

            if (mollierProcess is UndefinedProcess || mollierProcess is SpecificProcess)
            {
                return null;
            }

            // FanProcess derives from HeatingProcess, so it must be tested first.
            if (mollierProcess is FanProcess)
            {
                return typeof(SystemFan);
            }

            if (mollierProcess is HeatingProcess)
            {
                return typeof(SystemHeatingCoil);
            }

            if (mollierProcess is CoolingProcess)
            {
                return typeof(SystemCoolingCoil);
            }

            if (mollierProcess is HeatRecoveryProcess)
            {
                return typeof(SystemExchanger);
            }

            if (mollierProcess is HumidificationProcess)
            {
                return typeof(SystemHumidifier);
            }

            if (mollierProcess is MixingProcess)
            {
                return typeof(SystemAirJunction);
            }

            return null;
        }
    }
}

// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Core.Mollier;

namespace SAM.Analytical.Systems.Mollier
{
    public static partial class Query
    {
        public static double FanPressureRise(this FanProcess fanProcess, double fanEfficiency = 0.7)
        {
            if (fanProcess == null || double.IsNaN(fanEfficiency) || fanEfficiency <= 0)
            {
                return double.NaN;
            }

            MollierPoint start = fanProcess.Start;
            MollierPoint end = fanProcess.End;

            if (start == null || end == null || !start.IsValid() || !end.IsValid())
            {
                return double.NaN;
            }

            double deltaT = end.DryBulbTemperature - start.DryBulbTemperature;
            if (deltaT <= 0)
            {
                return double.NaN;
            }

            double density = start[MollierPointProperty.Density];
            if (double.IsNaN(density))
            {
                return double.NaN;
            }

            const double specificHeatCapacity = 1010.0;

            return density * specificHeatCapacity * deltaT / fanEfficiency;
        }
    }
}

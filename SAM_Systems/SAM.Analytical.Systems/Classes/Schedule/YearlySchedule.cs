// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Text.Json.Nodes;
using System.Linq;

namespace SAM.Analytical.Systems
{
    public class YearlySchedule : Schedule
    {
        private double[] values = new double[8760];

        public YearlySchedule()
        {

        }

        public YearlySchedule(string name)
            :base(name)
        {

        }

        public YearlySchedule(YearlySchedule yearlySchedule)
            :base(yearlySchedule)
        {
            if(yearlySchedule != null)
            {
                if (yearlySchedule.values != null)
                {
                    //Every hour of the year, not twenty-four of them. A yearly schedule holds 8760 values;
                    //copying only the first day left the other 8736 hours at zero - silently, so an annual
                    //operating schedule became one day of operation and every stage downstream reported
                    //success. The 24 was copied from ScheduleDay, where a day really is 24 hours.
                    int count = System.Math.Min(values.Length, yearlySchedule.values.Length);
                    for (int i = 0; i < count; i++)
                    {
                        values[i] = yearlySchedule.values[i];
                    }

                }
            }
        }

        public YearlySchedule(JsonObject jObject)
            :base(jObject)
        {

        }

        public double[] Values
        {
            get
            {
                return values?.ToArray();
            }

            set
            {
                if (value == null)
                {
                    values = new double[8760];
                }

                int count = value.Count();
                for (int i = 0; i < 8760; i++)
                {
                    values[i] = value[i % count];
                }
            }
        }

        public double this[int i]
        {
            get
            {
                return values[i];
            }

            set
            {
                values[i] = value;
            }
        }

        public override bool FromJsonObject(JsonObject jObject)
        {
            bool result = base.FromJsonObject(jObject);
            if(!result )
            {
                return result;
            }

            if (jObject.ContainsKey("Values"))
            {
                values = new double[8760];
                JsonArray jArray = jObject["Values"] as JsonArray;
                int count = jArray.Count;
                for (int i = 0; i < 8760; i++)
                {
                    values[i] = (double)jArray[i % count];
                }
            }

            return true;
        }

        public override JsonObject ToJsonObject()
        {
            JsonObject result = base.ToJsonObject();
            if(result == null)
            {
                return result;
            }

            if (values != null)
            {
                JsonArray jArray = new JsonArray();
                for (int i = 0; i < values.Length; i++)
                {
                    jArray.Add(values[i]);
                }

                result.Add("Values", jArray);
            }

            return result;
        }
    }
}

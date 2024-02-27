﻿namespace SAM.Analytical.Systems
{
    public static partial class Query
    {
        public static string DefaultPath(this AnalyticalSystemSettingParameter analyticalSystemSettingParameter)
        {
            if (!ActiveSetting.Setting.TryGetValue(analyticalSystemSettingParameter, out string name) || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string directory = ResourcesDirectory();
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            return global::System.IO.Path.Combine(directory, name);
        }
    }
}

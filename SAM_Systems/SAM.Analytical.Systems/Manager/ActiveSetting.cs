// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Geometry.Systems;
using System.Reflection;

namespace SAM.Analytical.Systems
{
    public static partial class ActiveSetting
    {
        public static class Name
        {
            //public const string ParameterMap = "Parameter Map";
        }

        private static Setting setting = null;

        private static Setting Load()
        {
            Setting setting = ActiveManager.GetSetting(Assembly.GetExecutingAssembly());
            if (setting == null)
            {
                setting = GetDefault();
            }

            return setting;
        }

        public static Setting Setting
        {
            get
            {
                if(setting == null)
                {
                    setting = Load();
                }

                return setting;
            }
        }

        public static Setting GetDefault()
        {
            Setting result = new Setting(Assembly.GetExecutingAssembly());

            result.SetValue(AnalyticalSystemSettingParameter.DefaultDisplaySystemManagerFileName, "SAM_DisplaySystemManager.JSON");
            result.SetValue(AnalyticalSystemSettingParameter.DefaultDisplaySystemManagerFileName, "SAM_DisplaySystemManager.JSON");
            result.SetValue(AnalyticalSystemSettingParameter.DefaultSystemEnergyCentreDirectoryName, "SystemEnergyCentre");
            result.SetValue(AnalyticalSystemSettingParameter.DefaultVentilationUnitDirectoryName, "VentilationUnit");

            string path = null;

            path = Query.DefaultPath(result, AnalyticalSystemSettingParameter.DefaultDisplaySystemManagerFileName);
            if (System.IO.File.Exists(path))
            {
                result.SetValue(AnalyticalSystemSettingParameter.DefaultDisplaySystemManager, Core.Create.IJSAMObject<DisplaySystemManager>(System.IO.File.ReadAllText(path)));
            }

            string directory = null;

            directory = Query.DefaultPath(result, AnalyticalSystemSettingParameter.DefaultSystemEnergyCentreDirectoryName);
            if (System.IO.Directory.Exists(directory))
            {
                result.SetValue(AnalyticalSystemSettingParameter.DefaultSystemEnergyCentreFileDirectory, directory);
            }

            directory = Query.DefaultPath(result, AnalyticalSystemSettingParameter.DefaultVentilationUnitDirectoryName);
            if (System.IO.Directory.Exists(directory))
            {
                result.SetValue(AnalyticalSystemSettingParameter.DefaultVentilationUnitFileDirectory, directory);
            }


            return result;
        }
    }
}

// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.IO;
using SAM.Core;
using SAM.Core.Systems;

namespace SAM.Analytical.Systems
{
    public static partial class Query
    {
        /// <summary>
        /// Loads a <see cref="Core.Systems.SystemEnergyCentre"/> from a SAM JSON file. SAM writes an energy centre as a
        /// top-level JSON array whose first element is the energy centre, so the first non-null
        /// <see cref="Core.Systems.SystemEnergyCentre"/> in the file is returned. Returns null when the path is missing or
        /// unreadable, or holds no energy centre.
        /// </summary>
        /// <param name="path">Full path to a SAM energy-centre JSON file (e.g. a plant-room template).</param>
        public static SystemEnergyCentre SystemEnergyCentre(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch
            {
                return null;
            }

            List<SystemEnergyCentre> systemEnergyCentres = Core.Create.IJSAMObjects<SystemEnergyCentre>(json);
            if (systemEnergyCentres == null)
            {
                return null;
            }

            foreach (SystemEnergyCentre systemEnergyCentre in systemEnergyCentres)
            {
                if (systemEnergyCentre != null)
                {
                    return systemEnergyCentre;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves the bundled SystemEnergyCentre resources directory (where the example templates such as
        /// MVRE.json and Plantroom-Only.json live), or null when it cannot be located.
        /// </summary>
        public static string DefaultSystemEnergyCentreDirectory()
        {
            Setting setting = ActiveSetting.Setting;

            string directory = setting?.GetValue<string>(AnalyticalSystemSettingParameter.DefaultSystemEnergyCentreFileDirectory);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                return directory;
            }

            string resourcesDirectory = ResourcesDirectory();
            if (string.IsNullOrWhiteSpace(resourcesDirectory))
            {
                return null;
            }

            string name = setting?.GetValue<string>(AnalyticalSystemSettingParameter.DefaultSystemEnergyCentreDirectoryName);
            directory = string.IsNullOrWhiteSpace(name) ? resourcesDirectory : Path.Combine(resourcesDirectory, name);

            return Directory.Exists(directory) ? directory : null;
        }

        /// <summary>
        /// Loads one of the bundled SystemEnergyCentre templates by file name (for example
        /// "Plantroom-Only.json") from the default resources directory. Returns null when the directory or file
        /// cannot be found.
        /// </summary>
        /// <param name="fileName">File name (with extension) of a bundled template.</param>
        public static SystemEnergyCentre SystemEnergyCentre_FromResources(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string directory = DefaultSystemEnergyCentreDirectory();
            if (string.IsNullOrWhiteSpace(directory))
            {
                return null;
            }

            return SystemEnergyCentre(Path.Combine(directory, fileName));
        }
    }
}

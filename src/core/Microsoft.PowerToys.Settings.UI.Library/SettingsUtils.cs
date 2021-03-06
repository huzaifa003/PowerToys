﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SettingsUtils : ISettingsUtils
    {
        private const string DefaultFileName = "settings.json";
        private const string DefaultModuleName = "";
        private IIOProvider _ioProvider;

        public SettingsUtils(IIOProvider ioProvider)
        {
            _ioProvider = ioProvider ?? throw new ArgumentNullException(nameof(ioProvider));
        }

        private bool SettingsFolderExists(string powertoy)
        {
            return _ioProvider.DirectoryExists(System.IO.Path.Combine(LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"));
        }

        private void CreateSettingsFolder(string powertoy)
        {
            _ioProvider.CreateDirectory(System.IO.Path.Combine(LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"));
        }

        public void DeleteSettings(string powertoy = "")
        {
            _ioProvider.DeleteDirectory(System.IO.Path.Combine(LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"));
        }

        /// <summary>
        /// Get path to the json settings file.
        /// </summary>
        /// <returns>string path.</returns>
        public static string GetSettingsPath(string powertoy, string fileName = DefaultFileName)
        {
            if (string.IsNullOrWhiteSpace(powertoy))
            {
                return System.IO.Path.Combine(
                    LocalApplicationDataFolder(),
                    $"Microsoft\\PowerToys\\{fileName}");
            }

            return System.IO.Path.Combine(
                LocalApplicationDataFolder(),
                $"Microsoft\\PowerToys\\{powertoy}\\{fileName}");
        }

        public bool SettingsExists(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
        {
            return _ioProvider.FileExists(GetSettingsPath(powertoy, fileName));
        }

        /// <summary>
        /// Get a Deserialized object of the json settings string.
        /// This function creates a file in the powertoy folder if it does not exist and returns an object with default properties.
        /// </summary>
        /// <returns>Deserialized json settings object.</returns>
        public T GetSettings<T>(string powertoy = DefaultModuleName, string fileName = DefaultFileName)
            where T : ISettingsConfig, new()
        {
            if (SettingsExists(powertoy, fileName))
            {
                // Given the file already exists, to deserialize the file and read it's content.
                T deserializedSettings = GetFile<T>(powertoy, fileName);

                // IF the file needs to be modified, to save the new configurations accordingly.
                if (deserializedSettings.UpgradeSettingsConfiguration())
                {
                    SaveSettings(deserializedSettings.ToJsonString(), powertoy, fileName);
                }

                return deserializedSettings;
            }
            else
            {
                // If the settings file does not exist, to create a new object with default parameters and save it to a newly created settings file.
                T newSettingsItem = new T();
                SaveSettings(newSettingsItem.ToJsonString(), powertoy, fileName);
                return newSettingsItem;
            }
        }

        // Given the powerToy folder name and filename to be accessed, this function deserializes and returns the file.
        private T GetFile<T>(string powertoyFolderName = DefaultModuleName, string fileName = DefaultFileName)
        {
            // Adding Trim('\0') to overcome possible NTFS file corruption.
            // Look at issue https://github.com/microsoft/PowerToys/issues/6413 you'll see the file has a large sum of \0 to fill up a 4096 byte buffer for writing to disk
            // This, while not totally ideal, does work around the problem by trimming the end.
            // The file itself did write the content correctly but something is off with the actual end of the file, hence the 0x00 bug
            var jsonSettingsString = _ioProvider.ReadAllText(GetSettingsPath(powertoyFolderName, fileName)).Trim('\0');
            return JsonSerializer.Deserialize<T>(jsonSettingsString);
        }

        // Save settings to a json file.
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "General exceptions will be logged until we can better understand runtime exception scenarios")]
        public void SaveSettings(string jsonSettings, string powertoy = DefaultModuleName, string fileName = DefaultFileName)
        {
            try
            {
                if (jsonSettings != null)
                {
                    if (!SettingsFolderExists(powertoy))
                    {
                        CreateSettingsFolder(powertoy);
                    }

                    _ioProvider.WriteAllText(GetSettingsPath(powertoy, fileName), jsonSettings);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception encountered while saving {powertoy} settings.", e);
#if DEBUG
                if (e is ArgumentException || e is ArgumentNullException || e is PathTooLongException)
                {
                    throw;
                }
#endif
            }
        }

        private static string LocalApplicationDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
    }
}

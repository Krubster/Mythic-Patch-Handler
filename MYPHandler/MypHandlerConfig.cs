using System;
using System.Configuration;

namespace MYPHandler
{
    public static class MypHandlerConfig
    {
        private static string maxOperationThread = "MaxOperationThread";
        private static string multithreadedExtraction = "MultiThreadedExtraction";

        public static int MaxOperationThread
        {
            get
            {
                if (ConfigurationManager.AppSettings[MypHandlerConfig.maxOperationThread] == null)
                    return 2;
                return Convert.ToInt32(ConfigurationManager.AppSettings[MypHandlerConfig.maxOperationThread]);
            }
            set
            {
                MypHandlerConfig.UpdateConfiguration(MypHandlerConfig.maxOperationThread, value.ToString());
            }
        }

        public static bool MultithreadedExtraction
        {
            get
            {
                if (ConfigurationManager.AppSettings[MypHandlerConfig.multithreadedExtraction] == null)
                    return true;
                return Convert.ToBoolean(ConfigurationManager.AppSettings[MypHandlerConfig.multithreadedExtraction]);
            }
            set
            {
                MypHandlerConfig.UpdateConfiguration(MypHandlerConfig.multithreadedExtraction, value.ToString());
            }
        }

        private static void UpdateConfiguration(string key, string value)
        {
            System.Configuration.Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            if (ConfigurationManager.AppSettings[key] != null)
                configuration.AppSettings.Settings.Remove(key);
            configuration.AppSettings.Settings.Add(key, value);
            configuration.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }

        private static void RemoveConfigurationKey(string key)
        {
            System.Configuration.Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            for (int index = 0; index < ConfigurationManager.AppSettings.Keys.Count; ++index)
            {
                if (ConfigurationManager.AppSettings.Keys.Get(index) == key)
                {
                    configuration.AppSettings.Settings.Remove(key);
                    break;
                }
            }
            configuration.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
        }
    }
}

using Sitecore.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Demo.Foundation.MediaLibrary.Helpers
{
    public class LogHelper
    {
        public static void LogInfo(string message)
        {
            if (!Sitecore.Context.IsUnitTesting)
            {
                if (Log.IsDebugEnabled)
                {
                    Log.Debug(message);
                }
                else
                {
                    Log.Info(message, typeof(LogHelper));
                }
            }
        }
    }
}
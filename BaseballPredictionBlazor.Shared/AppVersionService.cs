using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BaseballPredictionBlazor.Shared
{
    public class AppVersionService : IAppVersionService
    {
        public string Version =>
            Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
    }
}

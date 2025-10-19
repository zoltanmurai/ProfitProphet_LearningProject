using ProfitProphet.Settings;
using System;
using System.Threading.Tasks;
namespace ProfitProphet.Services
{
    public interface IAppSettingsService
    {
        //AppSettings Load();
        //void Save(AppSettings settings);
        AppSettings LoadSettings();
        Task SaveSettingsAsync(AppSettings settings);
    }
}

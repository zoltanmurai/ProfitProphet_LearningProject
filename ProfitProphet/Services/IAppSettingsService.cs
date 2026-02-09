using ProfitProphet.Settings;
using System;
using System.Threading.Tasks;
namespace ProfitProphet.Services
{
    public interface IAppSettingsService
    {
        //AppSettings Load();
        AppSettings CurrentSettings { get; }
        //void Save(AppSettings settings);
        AppSettings LoadSettings();
        Task SaveSettingsAsync(AppSettings settings);
    }
}

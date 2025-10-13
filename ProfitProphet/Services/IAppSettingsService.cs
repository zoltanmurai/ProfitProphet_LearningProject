using System;
namespace ProfitProphet.Services
{
    public interface IAppSettingsService
    {
        AppSettings Load();
        void Save(AppSettings settings);
    }
}
